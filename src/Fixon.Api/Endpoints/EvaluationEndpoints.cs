using System.Security.Claims;
using Fixon.Api.Services;
using Fixon.Infrastructure.Auth;
using Fixon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace Fixon.Api.Endpoints;

public static class EvaluationEndpoints
{
    [Authorize(Policy = Permissions.ClaimsManage)]
    public static async Task<IResult> RunEvaluation(
        UserContext userContext,
        ClaimsPrincipal user,
        SlaEvaluationService slaEvaluationService,
        CancellationToken ct)
    {
        var userIdClaim = user.FindFirst(FixonClaims.UserId)?.Value;
        var createdByUserId = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;
        if (createdByUserId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "User ID not found in token." });
        }

        var generated = await slaEvaluationService.RunEvaluationForTenant(userContext.TenantId, createdByUserId, ct);

        return Results.Ok(new { claimsGenerated = generated });
    }
}
