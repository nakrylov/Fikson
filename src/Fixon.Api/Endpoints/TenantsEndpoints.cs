using Fixon.Domain.Companies;
using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Fixon.Api.Endpoints;

public static class TenantsEndpoints
{
    /// <summary>
    /// POST /api/tenants
    /// Creates a new tenant (Company) and assigns current user as Admin via membership.
    /// Requires authentication, but does NOT require tenant context.
    /// </summary>
    public static async Task<IResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        ClaimsPrincipal user,
        FixonDbContext db,
        IJwtTokenService tokenService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name is required." });
        }

        var userIdRaw = user.FindFirst(FixonClaims.UserId)?.Value;
        if (string.IsNullOrWhiteSpace(userIdRaw) || !Guid.TryParse(userIdRaw, out var userId))
        {
            return Results.Unauthorized();
        }

        var dbUser = await db.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (dbUser is null || !dbUser.IsActive)
        {
            return Results.Unauthorized();
        }

        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();

        var company = new Company(
            id: tenantId,
            name: request.Name.Trim(),
            createdAt: now,
            isActive: true,
            planType: PlanType.Free,
            monthlyUsageLimit: request.MonthlyUsageLimit ?? 1000);

        db.Companies.Add(company);

        // Membership: current user becomes Admin/Active in this tenant.
        db.UserTenantMemberships.Add(new UserTenantMembership(
            id: Guid.NewGuid(),
            userId: dbUser.Id,
            tenantId: tenantId,
            role: MembershipRole.Admin,
            status: MembershipStatus.Active,
            createdAt: now));

        // Optional legacy assignment for compatibility.
        dbUser.AssignCompany(tenantId);

        await db.SaveChangesAsync(ct);

        var role = MembershipRole.Admin.ToString();
        var permissions = RolePermissionsMapping.GetPermissionsForRole(role);
        var roles = new List<string> { role };

        var token = tokenService.GenerateToken(dbUser, currentTenantId: tenantId, roles: roles, permissions: permissions);

        return Results.Ok(new
        {
            token,
            tenantId = tenantId,
            role = role
        });
    }
}

public sealed class CreateTenantRequest
{
    public string Name { get; set; } = null!;

    /// <summary>
    /// Optional override; defaults to 1000 for Free plan.
    /// </summary>
    public int? MonthlyUsageLimit { get; set; }
}

