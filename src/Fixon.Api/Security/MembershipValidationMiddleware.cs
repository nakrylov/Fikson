using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Api.Security;

/// <summary>
/// Validates that the authenticated user still has an ACTIVE membership
/// for the current tenant in the JWT (`tenant_id` claim).
///
/// This provides DB-backed enforcement of membership status without changing
/// existing permission-based policies.
/// </summary>
public sealed class MembershipValidationMiddleware
{
    private readonly RequestDelegate _next;

    public MembershipValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, FixonDbContext db)
    {
        var ct = context.RequestAborted;

        // Skip for anonymous requests.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null;
        if (allowAnonymous)
        {
            await _next(context);
            return;
        }

        var requireTenant = endpoint?.Metadata.GetMetadata<RequireTenantAttribute>() != null;

        var userIdRaw = context.User.FindFirst(FixonClaims.UserId)?.Value;

        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!requireTenant)
        {
            // Tenant-less JWT support: authenticated user without tenant context is allowed here.
            await _next(context);
            return;
        }

        var tenantIdRaw = context.User.FindFirst(FixonClaims.TenantId)?.Value;
        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // Ensure membership exists and is active for current tenant.
        var membership = await db.UserTenantMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId, ct);

        if (membership is null || membership.Status != MembershipStatus.Active)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }
}

