using Fixon.Application.Bootstrap;
using Fixon.Application.Bootstrap.Commands;
using Fixon.Application.Bootstrap.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Fixon.Api.Endpoints;

public static class BootstrapEndpoints
{
    public static async Task<IResult> ImportFact(
        [FromBody] ImportFactRequest request,
        IBootstrapUseCases useCases,
        CancellationToken ct)
    {
        var result = await useCases.ImportFactAsync(
            new ImportFactCommand(request.ExternalId, request.OccurredAtUtc, request.Value),
            ct);

        return Results.Ok(new { result.FactId, result.IsDuplicate });
    }

    public static async Task<IResult> CreateClaim(
        [FromBody] CreateClaimRequest request,
        IBootstrapUseCases useCases,
        CancellationToken ct)
    {
        var result = await useCases.CreateClaimAsync(new CreateClaimCommand(request.FactId), ct);
        return Results.Ok(new { result.ClaimId });
    }

    public static async Task<IResult> SubmitClaim(
        [FromRoute] Guid id,
        IBootstrapUseCases useCases,
        CancellationToken ct)
    {
        await useCases.SubmitClaimAsync(new SubmitClaimCommand(id), ct);
        return Results.Ok(new { claimId = id, status = "Submitted" });
    }

    public static async Task<IResult> GetPenaltyByClaimId(
        [FromRoute] Guid claimId,
        IBootstrapUseCases useCases,
        CancellationToken ct)
    {
        var result = await useCases.GetPenaltyByClaimIdAsync(new GetPenaltyByClaimIdQuery(claimId), ct);
        return result == null
            ? Results.NotFound()
            : Results.Ok(result);
    }
}

public sealed record ImportFactRequest(string ExternalId, DateTimeOffset OccurredAtUtc, decimal Value);
public sealed record CreateClaimRequest(Guid FactId);

