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
using Fixon.Domain.Companies;
using Fixon.Domain.Users;
using Fixon.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


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
    builder.Services.AddScoped<SlaEvaluationService>();

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

// Development-only seed: create a default tenant + admin user.
// IMPORTANT:
// - Only runs in Development (never in Production).
// - Only runs in FULL APP mode (bootstrap disabled), because auth services/endpoints
//   are not registered in bootstrap mode.
// - Uses hashed password (no plaintext stored in DB).
// - Uses SystemTenantProvider to avoid tenant filter issues during startup seeding.
if (app.Environment.IsDevelopment() && !bootstrapOptions.Enabled)
{
    using var scope = app.Services.CreateScope();
    await SeedDevelopmentDataAsync(scope.ServiceProvider, CancellationToken.None);
}

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
    // DB-backed check: membership must be active for current tenant.
    app.UseMiddleware<MembershipValidationMiddleware>();
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

    api.MapPost("/auth/register", AuthEndpoints.Register)
        .AllowAnonymous()
        .DisableRateLimiting();

    api.MapPost("/auth/switch-tenant", AuthEndpoints.SwitchTenant)
        .RequireAuthorization()
        .DisableRateLimiting();

    api.MapGet("/auth/me", AuthEndpoints.Me)
        .RequireAuthorization()
        .DisableRateLimiting();

    // Tenant creation (requires auth but NOT tenant context)
    api.MapPost("/tenants", TenantsEndpoints.CreateTenant)
        .RequireAuthorization()
        .DisableRateLimiting();

    // Public invite lookup (no tenant required)
    api.MapGet("/invites/{token}", InvitesEndpoints.GetInvite)
        .AllowAnonymous()
        .DisableRateLimiting();

    // Accept invite (authenticated identity, no tenant required)
    api.MapPost("/tenants/join", InvitesEndpoints.JoinTenant)
        .RequireAuthorization()
        .DisableRateLimiting();

    // Tenant-aware endpoints: require tenant_id + active membership.
    var tenantApi = api.MapGroup("")
        .WithMetadata(new RequireTenantAttribute());

    // Tenant admin: create invite
    tenantApi.MapPost("/tenants/{tenantId:guid}/invites", InvitesEndpoints.CreateInvite)
        .RequireAuthorization();

    // Tenant admin: list/revoke invites
    tenantApi.MapGet("/tenants/{tenantId:guid}/invites", InvitesEndpoints.GetTenantInvites)
        .RequireAuthorization();
    tenantApi.MapPost("/tenants/{tenantId:guid}/invites/{inviteId:guid}/revoke", InvitesEndpoints.RevokeInvite)
        .RequireAuthorization();

    // Tenant admin: list memberships
    tenantApi.MapGet("/tenants/{tenantId:guid}/members", TenantsEndpoints.GetTenantMembers)
        .RequireAuthorization();

    // Protected endpoints
    tenantApi.MapGet("/contracts", ContractsEndpoints.GetContracts)
        .RequireAuthorization(Permissions.ContractsRead);

    tenantApi.MapGet("/contracts/{contractId:guid}", ContractsEndpoints.GetContract)
        .RequireAuthorization(Permissions.ContractsRead);
    tenantApi.MapGet("/contracts/{contractId:guid}/dashboard", ContractsEndpoints.GetDashboard)
        .RequireAuthorization(Permissions.ContractsRead);

    tenantApi.MapPost("/contracts", ContractsEndpoints.CreateContract)
        .RequireAuthorization(Permissions.ContractsManage);

    tenantApi.MapPut("/contracts/{contractId:guid}", ContractsEndpoints.UpdateContract)
        .RequireAuthorization(Permissions.ContractsManage);

    tenantApi.MapDelete("/contracts/{contractId:guid}", ContractsEndpoints.DeleteContract)
        .RequireAuthorization(Permissions.ContractsManage);

    tenantApi.MapGet("/contracts/{contractId:guid}/history", ContractsEndpoints.GetContractHistory)
        .RequireAuthorization(Permissions.ContractsRead);

    tenantApi.MapPost("/contracts/{contractId:guid}/versions", ContractsEndpoints.CreateContractVersion)
        .RequireAuthorization(Permissions.ContractsManage);

    tenantApi.MapPost("/contracts/{contractId:guid}/versions/{versionId:guid}/sign", ContractsEndpoints.SignContractVersion)
        .RequireAuthorization(Permissions.ContractsManage);

    tenantApi.MapPost("/contracts/{contractId:guid}/versions/{versionId:guid}/activate", ContractsEndpoints.ActivateContractVersion)
        .RequireAuthorization(Permissions.ContractsManage);

    tenantApi.MapPost("/contracts/{contractId:guid}/terminate", ContractsEndpoints.TerminateContract)
        .RequireAuthorization(Permissions.ContractsManage);

    // Evaluation
    tenantApi.MapPost("/evaluation/run", EvaluationEndpoints.RunEvaluation)
        .RequireAuthorization(Permissions.ClaimsManage);

    // Reporting endpoints (read-only)
    tenantApi.MapGet("/reports/sla-compliance", ReportsEndpoints.GetSlaCompliance)
        .RequireAuthorization(Permissions.SlaView);

    tenantApi.MapGet("/reports/penalties-summary", ReportsEndpoints.GetPenaltiesSummary)
        .RequireAuthorization(Permissions.ClaimsRead);

    tenantApi.MapGet("/reports/audit", ReportsEndpoints.GetAuditFeed)
        .RequireAuthorization(Permissions.AuditRead);

    // Imports (two-phase: upload → validate → commit)
    tenantApi.MapPost("/imports/facts", FactsImportEndpoints.ImportFacts)
        .RequireAuthorization(Permissions.ImportsUpload)
        .DisableAntiforgery();
    tenantApi.MapGet("/imports", FactsImportEndpoints.GetImportHistory)
        .RequireAuthorization(Permissions.ImportsUpload);

    tenantApi.MapPost("/imports/facts/upload", ImportsEndpoints.UploadFactsImport)
        .RequireAuthorization(Permissions.ImportsUpload);

    tenantApi.MapPost("/imports/{batchId:guid}/validate", ImportsEndpoints.ValidateImport)
        .RequireAuthorization(Permissions.ImportsUpload);

    tenantApi.MapPost("/imports/{batchId:guid}/commit", ImportsEndpoints.CommitImport)
        .RequireAuthorization(Permissions.ImportsUpload);

    tenantApi.MapGet("/imports/{batchId:guid}", ImportsEndpoints.GetImportStatus)
        .RequireAuthorization(Permissions.ImportsUpload);

    tenantApi.MapGet("/imports/{batchId:guid}/errors", ImportsEndpoints.GetImportErrors)
        .RequireAuthorization(Permissions.ImportsUpload);

    // Claims lifecycle
    tenantApi.MapGet("/claims", ClaimsEndpoints.GetClaims)
        .RequireAuthorization(Permissions.ClaimsRead);
    tenantApi.MapGet("/claims/summary", ClaimsEndpoints.GetClaimsSummary)
        .RequireAuthorization(Permissions.ClaimsRead);

    tenantApi.MapGet("/claims/{claimId:guid}", ClaimsEndpoints.GetClaim)
        .RequireAuthorization(Permissions.ClaimsRead);

    tenantApi.MapGet("/claims/{claimId:guid}/timeline", ClaimsEndpoints.GetClaimTimeline)
        .RequireAuthorization(Permissions.ClaimsRead);

    tenantApi.MapPost("/claims/{claimId:guid}/submit", ClaimsEndpoints.SubmitClaim)
        .RequireAuthorization(Permissions.ClaimsManage);

    tenantApi.MapPost("/claims/{claimId:guid}/review", ClaimsEndpoints.ReviewClaim)
        .RequireAuthorization(Permissions.ClaimsManage);

    tenantApi.MapPost("/claims/{claimId:guid}/decide", ClaimsEndpoints.DecideClaim)
        .RequireAuthorization(Permissions.ClaimsManage);

    tenantApi.MapPost("/claims/{claimId:guid}/disputes/open", ClaimsEndpoints.OpenDispute)
        .RequireAuthorization(Permissions.ClaimsManage);

    tenantApi.MapPost("/claims/{claimId:guid}/disputes/{disputeId:guid}/resolve", ClaimsEndpoints.ResolveDispute)
        .RequireAuthorization(Permissions.ClaimsManage);

    tenantApi.MapPost("/claims/{claimId:guid}/cancel", ClaimsEndpoints.CancelClaim)
        .RequireAuthorization(Permissions.ClaimsManage);
}

app.Run();

static async Task SeedDevelopmentDataAsync(IServiceProvider services, CancellationToken ct)
{
    // Keep all dev-only credentials here, inside Development branch only.
    const string adminEmail = "admin@fixon.local";
    const string adminPassword = "Admin123!";
    const string defaultTenantName = "Default Tenant";

    var cfg = services.GetRequiredService<IConfiguration>();
    var connectionString = cfg.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        // In Development we allow the existing fallback from Program.cs DbContext registration.
        // If you want a specific DB, set ConnectionStrings__DefaultConnection.
        connectionString = "Host=localhost;Port=5432;Database=fixon;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true";
    }

    var options = new DbContextOptionsBuilder<FixonDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    // System context so query filters won't block reads/writes during seeding.
    await using var db = new FixonDbContext(options, new SystemTenantProvider());

    // Ensure schema is up to date in dev (idempotent).
    await db.Database.MigrateAsync(ct);

    // If user already exists, we still ensure membership exists (idempotent on startup).
    var existing = await db.Users
        .IgnoreQueryFilters()
        .SingleOrDefaultAsync(u => u.Email == adminEmail, ct);

    var now = DateTimeOffset.UtcNow;

    // Ensure default tenant (company).
    var company = await db.Companies
        .IgnoreQueryFilters()
        .SingleOrDefaultAsync(c => c.Name == defaultTenantName, ct);

    if (company is null)
    {
        company = new Company(Guid.NewGuid(), defaultTenantName, now, isActive: true);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
    }

    // Ensure Admin role exists.
    var adminRole = await db.Roles.SingleOrDefaultAsync(r => r.Name == Roles.Admin, ct);
    if (adminRole is null)
    {
        adminRole = new Role(Guid.NewGuid(), Roles.Admin);
        db.Roles.Add(adminRole);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // In case of concurrent startup, role could be created by another instance.
            db.ChangeTracker.Clear();
            adminRole = await db.Roles.SingleAsync(r => r.Name == Roles.Admin, ct);
        }
    }

    // Hash password (no plaintext stored in DB).
    // Use the same hasher implementation as the app uses.
    var passwordHash = new PasswordHasher().HashPassword(adminPassword);

    var user = existing;
    if (user is null)
    {
        var userId = Guid.NewGuid();
        user = new User(
            id: userId,
            companyId: company.Id,
            email: adminEmail,
            name: "Development Admin",
            passwordHash: passwordHash,
            createdAt: now,
            isActive: true);

        db.Users.Add(user);
        db.UserRoles.Add(new UserRole(userId: userId, roleId: adminRole.Id));
        await db.SaveChangesAsync(ct);
    }

    // Ensure default membership exists (Admin + Active).
    var membershipExists = await db.UserTenantMemberships
        .AsNoTracking()
        .AnyAsync(m => m.UserId == user.Id && m.TenantId == company.Id, ct);

    if (!membershipExists)
    {
        db.UserTenantMemberships.Add(new UserTenantMembership(
            id: Guid.NewGuid(),
            userId: user.Id,
            tenantId: company.Id,
            role: MembershipRole.Admin,
            status: MembershipStatus.Active,
            createdAt: now));

        await db.SaveChangesAsync(ct);
    }
}

public partial class Program { }
