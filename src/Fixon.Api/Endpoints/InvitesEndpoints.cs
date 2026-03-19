using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using Fixon.Application.Emails;
using Fixon.Api.Security;
using Fixon.Domain.Companies;
using Fixon.Domain.Users;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fixon.Api.Endpoints;

public static class InvitesEndpoints
{
    /// <summary>
    /// GET /api/tenants/{tenantId}/invites
    /// Lists invites for current tenant. RequireTenant: YES + Admin-only.
    /// </summary>
    [Authorize]
    [RequireTenant]
    public static async Task<IResult> GetTenantInvites(
        [FromRoute] Guid tenantId,
        ClaimsPrincipal user,
        FixonDbContext db,
        CancellationToken ct)
    {
        var currentTenantRaw = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (!Guid.TryParse(currentTenantRaw, out var currentTenantId) || currentTenantId != tenantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var roleClaim = user.FindFirst(FixonClaims.Role)?.Value;
        if (!string.Equals(roleClaim, MembershipRole.Admin.ToString(), StringComparison.Ordinal))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var invites = await db.TenantInvites
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new TenantInviteListItem
            {
                Id = i.Id,
                Email = i.Email,
                Role = i.Role.ToString(),
                Status = i.Status.ToString(),
                Token = i.Token,
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt
            })
            .ToListAsync(ct);

        return Results.Ok(invites);
    }

    /// <summary>
    /// POST /api/tenants/{tenantId}/invites/{inviteId}/revoke
    /// Revokes a pending invite. RequireTenant: YES + Admin-only.
    /// </summary>
    [Authorize]
    [RequireTenant]
    public static async Task<IResult> RevokeInvite(
        [FromRoute] Guid tenantId,
        [FromRoute] Guid inviteId,
        ClaimsPrincipal user,
        FixonDbContext db,
        CancellationToken ct)
    {
        var currentTenantRaw = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (!Guid.TryParse(currentTenantRaw, out var currentTenantId) || currentTenantId != tenantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var roleClaim = user.FindFirst(FixonClaims.Role)?.Value;
        if (!string.Equals(roleClaim, MembershipRole.Admin.ToString(), StringComparison.Ordinal))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var invite = await db.TenantInvites
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.Id == inviteId && i.TenantId == tenantId, ct);

        if (invite is null)
        {
            return Results.NotFound();
        }

        if (invite.Status != InviteStatus.Pending)
        {
            return Results.BadRequest(new { error = "ONLY_PENDING_CAN_BE_REVOKED" });
        }

        invite.MarkRevoked();
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    /// <summary>
    /// POST /api/tenants/{tenantId}/invites
    /// Creates an invite for a specific email address.
    /// RequireTenant: YES (mapped via tenantApi group) + Admin-only.
    /// </summary>
    [Authorize]
    [RequireTenant]
    public static async Task<IResult> CreateInvite(
        [FromRoute] Guid tenantId,
        [FromBody] CreateInviteRequest request,
        ClaimsPrincipal user,
        FixonDbContext db,
        IEmailService emailService,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        // Ensure token tenant matches route tenant (no cross-tenant creation).
        var currentTenantRaw = user.FindFirst(FixonClaims.TenantId)?.Value;
        if (!Guid.TryParse(currentTenantRaw, out var currentTenantId) || currentTenantId != tenantId)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        // Admin-only (membership role).
        var roleClaim = user.FindFirst(FixonClaims.Role)?.Value;
        if (!string.Equals(roleClaim, MembershipRole.Admin.ToString(), StringComparison.Ordinal))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
        {
            return Results.BadRequest(new { error = "Invalid email." });
        }

        if (request.Role is not (MembershipRole.Admin or MembershipRole.Member or MembershipRole.Viewer))
        {
            return Results.BadRequest(new { error = "Invalid role." });
        }

        // Check that user with this email is not already a member of this tenant.
        var existingUser = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email, ct);

        if (existingUser is not null)
        {
            var alreadyMember = await db.UserTenantMemberships
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(m => m.UserId == existingUser.Id && m.TenantId == tenantId, ct);

            if (alreadyMember)
            {
                return Results.Conflict(new { error = "ALREADY_MEMBER" });
            }
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(7);
        var token = GenerateSecureTokenBase64Url(32);

        var invite = new TenantInvite(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            email: request.Email.Trim(),
            role: request.Role,
            token: token,
            status: InviteStatus.Pending,
            createdAt: now,
            expiresAt: expiresAt);

        db.TenantInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        var frontendBaseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var joinLink = $"{frontendBaseUrl}/join?token={token}";
        var subject = "You were invited to Fixon";
        var body = $"You were invited to Fixon.{Environment.NewLine}{Environment.NewLine}Join link: {joinLink}";

        try
        {
            using var emailCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            emailCts.CancelAfter(TimeSpan.FromSeconds(15));
            await emailService.SendAsync(invite.Email, subject, body, emailCts.Token);
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger("InviteEmail");
            logger.LogWarning(ex, "Failed to send invite email to {Email} for tenant {TenantId}", invite.Email, tenantId);
        }

        return Results.Ok(new
        {
            inviteToken = token,
            expiresAt
        });
    }

    /// <summary>
    /// GET /api/invites/{token}
    /// Public endpoint: returns metadata for a pending, non-expired invite.
    /// </summary>
    [AllowAnonymous]
    public static async Task<IResult> GetInvite(
        [FromRoute] string token,
        FixonDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.NotFound();
        }

        var invite = await db.TenantInvites
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Token == token, ct);

        if (invite is null)
        {
            return Results.NotFound();
        }

        if (invite.Status == InviteStatus.Accepted)
        {
            return Results.Conflict(new { error = "INVITE_ACCEPTED" });
        }

        if (invite.Status != InviteStatus.Pending)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        if (invite.ExpiresAt <= now)
        {
            return Results.StatusCode(StatusCodes.Status410Gone);
        }

        var tenant = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == invite.TenantId, ct);

        if (tenant is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            tenantName = tenant.Name,
            email = invite.Email,
            role = invite.Role.ToString(),
            expiresAt = invite.ExpiresAt
        });
    }

    /// <summary>
    /// POST /api/tenants/join
    /// Accepts an invite and creates a membership.
    /// Requires authentication, but does NOT require tenant context.
    /// </summary>
    [Authorize]
    public static async Task<IResult> JoinTenant(
        [FromBody] JoinTenantRequest request,
        ClaimsPrincipal principal,
        FixonDbContext db,
        IJwtTokenService tokenService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new { error = "Token is required." });
        }

        var userIdRaw = principal.FindFirst(FixonClaims.UserId)?.Value;
        if (string.IsNullOrWhiteSpace(userIdRaw) || !Guid.TryParse(userIdRaw, out var userId))
        {
            return Results.Unauthorized();
        }

        // Load user (global identity).
        var user = await db.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Lock invite row to prevent double-accept.
        var invite = await db.TenantInvites
            .FromSqlInterpolated($@"
SELECT ""Id"", ""TenantId"", ""Email"", ""Role"", ""Token"", ""Status"", ""CreatedAt"", ""ExpiresAt"", ""AcceptedAt""
FROM tenant_invites
WHERE ""Token"" = {request.Token}
FOR UPDATE")
            .SingleOrDefaultAsync(ct);

        if (invite is null)
        {
            return Results.NotFound();
        }

        var now = DateTimeOffset.UtcNow;

        if (invite.Status == InviteStatus.Accepted)
        {
            return Results.Conflict(new { error = "INVITE_ACCEPTED" });
        }

        if (invite.Status != InviteStatus.Pending)
        {
            return Results.NotFound();
        }

        if (invite.ExpiresAt <= now)
        {
            // Mark expired (best-effort) and return 410.
            db.Entry(invite).Property(nameof(TenantInvite.Status)).CurrentValue = InviteStatus.Expired;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.StatusCode(StatusCodes.Status410Gone);
        }

        // Email-bound security: invite.email must equal user.email.
        if (!string.Equals(invite.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        // Ensure membership does not already exist.
        var membershipExists = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(m => m.UserId == user.Id && m.TenantId == invite.TenantId, ct);

        if (membershipExists)
        {
            return Results.Conflict(new { error = "ALREADY_MEMBER" });
        }

        // Create membership.
        db.UserTenantMemberships.Add(new UserTenantMembership(
            id: Guid.NewGuid(),
            userId: user.Id,
            tenantId: invite.TenantId,
            role: invite.Role,
            status: MembershipStatus.Active,
            createdAt: now));

        // Update invite.
        invite.MarkAccepted(now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Return new JWT with tenant context.
        var role = invite.Role.ToString();
        var permissions = RolePermissionsMapping.GetPermissionsForRole(role);
        var roles = new List<string> { role };
        var token = tokenService.GenerateToken(user, invite.TenantId, roles, permissions);

        return Results.Ok(new { token, tenantId = invite.TenantId, role });
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateSecureTokenBase64Url(int bytes)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        var s = Convert.ToBase64String(data);
        // base64url (no padding)
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public sealed class CreateInviteRequest
{
    public string Email { get; set; } = null!;
    public MembershipRole Role { get; set; } = MembershipRole.Member;
}

public sealed class JoinTenantRequest
{
    public string Token { get; set; } = null!;
}

public sealed class TenantInviteListItem
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

