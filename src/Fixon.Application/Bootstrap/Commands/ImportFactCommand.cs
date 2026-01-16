namespace Fixon.Application.Bootstrap.Commands;

public sealed record ImportFactCommand(
    string ExternalId,
    DateTimeOffset OccurredAtUtc,
    decimal Value);

public sealed record ImportFactResult(
    Guid FactId,
    bool IsDuplicate);

