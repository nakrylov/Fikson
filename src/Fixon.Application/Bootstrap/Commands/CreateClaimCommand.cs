namespace Fixon.Application.Bootstrap.Commands;

public sealed record CreateClaimCommand(Guid FactId);

public sealed record CreateClaimResult(
    Guid ClaimId,
    Guid PenaltyClaimId);

