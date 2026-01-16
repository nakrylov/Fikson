namespace Fixon.Application.Bootstrap.Queries;

public sealed record GetPenaltyByClaimIdQuery(Guid ClaimId);

public sealed record GetPenaltyByClaimIdResult(
    Guid ClaimId,
    decimal Amount,
    string Currency);

