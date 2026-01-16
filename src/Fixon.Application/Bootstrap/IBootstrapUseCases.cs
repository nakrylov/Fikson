using Fixon.Application.Bootstrap.Commands;
using Fixon.Application.Bootstrap.Queries;

namespace Fixon.Application.Bootstrap;

public interface IBootstrapUseCases
{
    Task<ImportFactResult> ImportFactAsync(ImportFactCommand command, CancellationToken ct);
    Task<CreateClaimResult> CreateClaimAsync(CreateClaimCommand command, CancellationToken ct);
    Task SubmitClaimAsync(SubmitClaimCommand command, CancellationToken ct);
    Task<GetPenaltyByClaimIdResult?> GetPenaltyByClaimIdAsync(GetPenaltyByClaimIdQuery query, CancellationToken ct);
}

