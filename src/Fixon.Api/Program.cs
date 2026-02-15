using Fixon.Api.Endpoints;
using Fixon.Api.Bootstrap;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Audit;
using Fixon.Infrastructure.BackgroundJobs;
using Fixon.Infrastructure.Persistence;
using Fixon.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Fixon.Api.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Threading.RateLimiting;
using Fixon.Infrastructure.Claims;
using Fixon.Api.Security;
using Fixon.Application.Bootstrap;
using Fixon.Infrastructure.Bootstrap;


var builder = WebApplication.CreateBuilder(args);

var bootstrapOptions = builder.Configuration.GetSection("Bootstrap").Get<BootstrapOptions>() ?? new BootstrapOptions();

// Observability (logging + tracing + metrics)
builder.Services.AddFixonObservability(builder.Configuration);

// OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fixon API",
        Version = "v1",
        Description = "Fixon API (bootstrap + full app endpoints)"
    });

    // JWT bearer (optional) so Swagger UI can call protected endpoints
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Example: \"Bearer {token}\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// HttpContextAccessor нужен для tenant providers
builder.Services.AddHttpContextAccessor();

if (bootstrapOptions.Enabled)
{
    // Strict vertical slice: single tenant comes from server config, not from API input.
    builder.Services.AddScoped<ITenantProvider>(_ => new FixedTenantProvider(bootstrapOptions.TenantId, isSystemContext: false));
    builder.Services.AddBootstrapVerticalSlice(bootstrapOptions);
    builder.Services.AddHostedService<BootstrapHostedService>();
}
else
{
    // Full app mode
    builder.Services.AddTenancyWithPublicEndpoints();
    builder.Services.AddFixonAuthentication(builder.Configuration);

    // Background jobs (hosted polling worker)
    builder.Services.AddBackgroundJobs(options =>
    {
        options.PollInterval = TimeSpan.FromSeconds(5);
        options.WorkerId = Environment.MachineName;
    });
}

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FixonDbContext>("db")
    .AddCheck<EfMigrationsHealthCheck>("db_migrations");

// Rate limiting (per tenant)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("per-tenant", httpContext =>
    {
        // tenant-aware key: validated JWT claim wins; fallback to header; else "anonymous"
        var tenantId =
            httpContext.User.FindFirst(Fixon.Infrastructure.Auth.FixonClaims.TenantId)?.Value
            ?? httpContext.Request.Headers["X-Tenant-Id"].ToString()
            ?? "anonymous";

        return RateLimitPartition.GetTokenBucketLimiter(tenantId, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 120,
            TokensPerPeriod = 120,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueLimit = 0
        });
    });
});

// DbContext (для login endpoint)
// ВАЖНО: Для login используем SystemTenantProvider через DbContext factory,
// чтобы обойти tenant filter и найти пользователя по email независимо от tenant
builder.Services.AddSingleton<AuditImmutabilityInterceptor>();
builder.Services.AddSingleton<ClaimDecisionImmutabilityInterceptor>();
builder.Services.AddScoped<TenantWriteGuardInterceptor>();

builder.Services.AddDbContext<FixonDbContext>((sp, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        // Local dev default. If you use managed Postgres with SSL, override via:
        // - appsettings.Development.json ConnectionStrings:DefaultConnection
        // - or environment variable ConnectionStrings__DefaultConnection
        connectionString = "Host=localhost;Port=5432;Database=fixon;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true";
    }
    options.UseNpgsql(connectionString);

    // Audit is append-only
    options.AddInterceptors(sp.GetRequiredService<AuditImmutabilityInterceptor>());
    // Claim decisions are append-only
    options.AddInterceptors(sp.GetRequiredService<ClaimDecisionImmutabilityInterceptor>());
    // Hard guard against cross-tenant writes
    options.AddInterceptors(sp.GetRequiredService<TenantWriteGuardInterceptor>());
});

// Регистрируем DbContext factory с tenant provider
builder.Services.AddScoped<FixonDbContext>(sp =>
{
    var options = sp.GetRequiredService<DbContextOptions<FixonDbContext>>();
    var tenantProvider = sp.GetRequiredService<ITenantProvider>();
    return new FixonDbContext(options, tenantProvider);
});

if (!bootstrapOptions.Enabled)
{
    // Регистрируем UserContext для использования в endpoints
    builder.Services.AddScoped<UserContext>(sp =>
    {
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        return UserContext.FromClaimsPrincipal(httpContext.User);
    });
}

var app = builder.Build();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fixon API v1");
        options.RoutePrefix = "swagger";
    });
}

// Correlation + structured log scopes (no PII)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingScopeMiddleware>();

app.UseRateLimiter();

if (!bootstrapOptions.Enabled)
{
    // Authentication middleware (должен быть до authorization)
    app.UseAuthentication();
}

// Tenant resolution middleware
app.UseMiddleware<TenantResolutionMiddleware>();

if (!bootstrapOptions.Enabled)
{
    app.UseAuthorization();
    app.UseMiddleware<AuthorizationAuditMiddleware>();
}

// Public endpoints
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Text("OK"));
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false, // liveness only
    ResponseWriter = async (ctx, _) => await ctx.Response.WriteAsync("OK")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
            })
        });
        await ctx.Response.WriteAsync(payload);
    }
});

if (bootstrapOptions.Enabled)
{
    // Strict vertical slice endpoints (no /api prefix, per prompt)
    app.MapPost("/facts", BootstrapEndpoints.ImportFact);
    app.MapPost("/claims", BootstrapEndpoints.CreateClaim);
    app.MapPost("/claims/{id:guid}/submit", BootstrapEndpoints.SubmitClaim);
    app.MapGet("/penalties/{claimId:guid}", BootstrapEndpoints.GetPenaltyByClaimId);
}
else
{
    // API group (rate limited by tenant)
    var api = app.MapGroup("/api")
        .RequireRateLimiting("per-tenant");

    // Auth endpoints (public, no rate limit)
    api.MapPost("/auth/login", AuthEndpoints.Login)
        .AllowAnonymous()
        .DisableRateLimiting();

    // Protected endpoints
    api.MapGet("/contracts", ContractsEndpoints.GetContracts)
        .RequireAuthorization(Permissions.ContractsRead);

    api.MapGet("/contracts/{contractId:guid}", ContractsEndpoints.GetContract)
        .RequireAuthorization(Permissions.ContractsRead);

    api.MapPost("/contracts", ContractsEndpoints.CreateContract)
        .RequireAuthorization(Permissions.ContractsManage);

    api.MapPut("/contracts/{contractId:guid}", ContractsEndpoints.UpdateContract)
        .RequireAuthorization(Permissions.ContractsManage);

    api.MapDelete("/contracts/{contractId:guid}", ContractsEndpoints.DeleteContract)
        .RequireAuthorization(Permissions.ContractsManage);

    api.MapGet("/contracts/{contractId:guid}/history", ContractsEndpoints.GetContractHistory)
        .RequireAuthorization(Permissions.ContractsRead);

    api.MapPost("/contracts/{contractId:guid}/versions", ContractsEndpoints.CreateContractVersion)
        .RequireAuthorization(Permissions.ContractsManage);

    api.MapPost("/contracts/{contractId:guid}/versions/{versionId:guid}/sign", ContractsEndpoints.SignContractVersion)
        .RequireAuthorization(Permissions.ContractsManage);

    api.MapPost("/contracts/{contractId:guid}/versions/{versionId:guid}/activate", ContractsEndpoints.ActivateContractVersion)
        .RequireAuthorization(Permissions.ContractsManage);

    api.MapPost("/contracts/{contractId:guid}/terminate", ContractsEndpoints.TerminateContract)
        .RequireAuthorization(Permissions.ContractsManage);

    // Reporting endpoints (read-only)
    api.MapGet("/reports/sla-compliance", ReportsEndpoints.GetSlaCompliance)
        .RequireAuthorization(Permissions.SlaView);

    api.MapGet("/reports/penalties-summary", ReportsEndpoints.GetPenaltiesSummary)
        .RequireAuthorization(Permissions.ClaimsRead);

    api.MapGet("/reports/audit", ReportsEndpoints.GetAuditFeed)
        .RequireAuthorization(Permissions.AuditRead);

    // Imports (two-phase: upload → validate → commit)
    api.MapPost("/imports/facts/upload", ImportsEndpoints.UploadFactsImport)
        .RequireAuthorization(Permissions.ImportsUpload);

    api.MapPost("/imports/{batchId:guid}/validate", ImportsEndpoints.ValidateImport)
        .RequireAuthorization(Permissions.ImportsUpload);

    api.MapPost("/imports/{batchId:guid}/commit", ImportsEndpoints.CommitImport)
        .RequireAuthorization(Permissions.ImportsUpload);

    api.MapGet("/imports/{batchId:guid}", ImportsEndpoints.GetImportStatus)
        .RequireAuthorization(Permissions.ImportsUpload);

    api.MapGet("/imports/{batchId:guid}/errors", ImportsEndpoints.GetImportErrors)
        .RequireAuthorization(Permissions.ImportsUpload);

    // Claims lifecycle
    api.MapGet("/claims/{claimId:guid}", ClaimsEndpoints.GetClaim)
        .RequireAuthorization(Permissions.ClaimsRead);

    api.MapGet("/claims/{claimId:guid}/timeline", ClaimsEndpoints.GetClaimTimeline)
        .RequireAuthorization(Permissions.ClaimsRead);

    api.MapPost("/claims/{claimId:guid}/submit", ClaimsEndpoints.SubmitClaim)
        .RequireAuthorization(Permissions.ClaimsManage);

    api.MapPost("/claims/{claimId:guid}/review", ClaimsEndpoints.ReviewClaim)
        .RequireAuthorization(Permissions.ClaimsManage);

    api.MapPost("/claims/{claimId:guid}/decide", ClaimsEndpoints.DecideClaim)
        .RequireAuthorization(Permissions.ClaimsManage);

    api.MapPost("/claims/{claimId:guid}/disputes/open", ClaimsEndpoints.OpenDispute)
        .RequireAuthorization(Permissions.ClaimsManage);

    api.MapPost("/claims/{claimId:guid}/disputes/{disputeId:guid}/resolve", ClaimsEndpoints.ResolveDispute)
        .RequireAuthorization(Permissions.ClaimsManage);

    api.MapPost("/claims/{claimId:guid}/cancel", ClaimsEndpoints.CancelClaim)
        .RequireAuthorization(Permissions.ClaimsManage);
}

app.Run();


public partial class Program { }
