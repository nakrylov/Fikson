using Fixon.Application.Bootstrap;
using Fixon.Application.Bootstrap.Commands;
using Fixon.Application.Bootstrap.Queries;
using Fixon.Domain.Claims;
using Fixon.Domain.Facts;
using Fixon.Domain.Financial;
using Fixon.Domain.Penalties;
using Fixon.Domain.Sla;
using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fixon.Infrastructure.Bootstrap;

public sealed class BootstrapUseCases : IBootstrapUseCases
{
    private readonly FixonDbContext _db;
    private readonly BootstrapOptions _options;

    public BootstrapUseCases(FixonDbContext db, BootstrapOptions options)
    {
        _db = db;
        _options = options;
    }

    public async Task<ImportFactResult> ImportFactAsync(ImportFactCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ExternalId)) throw new ArgumentException(nameof(command.ExternalId));

        var now = DateTimeOffset.UtcNow;
        var attributesJson = JsonSerializer.Serialize(new { value = command.Value });
        var payloadHash = Sha256Hex(attributesJson);

        var fact = new Fact(
            id: Guid.NewGuid(),
            companyId: _options.TenantId,
            contractId: _options.ContractId,
            externalReference: command.ExternalId,
            factType: "demo.value",
            attributesJson: attributesJson,
            occurredAt: command.OccurredAtUtc,
            createdAt: now,
            importBatchId: null,
            payloadHash: payloadHash);

        _db.Facts.Add(fact);

        try
        {
            await _db.SaveChangesAsync(ct);
            return new ImportFactResult(fact.Id, IsDuplicate: false);
        }
        catch (DbUpdateException)
        {
            // Idempotency key conflict: return existing
            var existing = await _db.Facts.AsNoTracking()
                .Where(f => f.ExternalReference == command.ExternalId && f.OccurredAt == command.OccurredAtUtc)
                .Select(f => f.Id)
                .FirstOrDefaultAsync(ct);

            if (existing == Guid.Empty) throw;
            return new ImportFactResult(existing, IsDuplicate: true);
        }
    }

    public async Task<CreateClaimResult> CreateClaimAsync(CreateClaimCommand command, CancellationToken ct)
    {
        var fact = await _db.Facts.AsNoTracking().SingleOrDefaultAsync(f => f.Id == command.FactId, ct);
        if (fact == null) throw new InvalidOperationException("Fact not found.");

        var value = ExtractValue(fact.AttributesJson);
        if (value <= _options.ThresholdValue)
        {
            throw new InvalidOperationException("No SLA violation: fact.Value <= threshold.");
        }

        var now = DateTimeOffset.UtcNow;

        // Create minimal SLA evaluation + violation (legal traceability)
        var evaluationId = Guid.NewGuid();
        var calculatedValuesJson = JsonSerializer.Serialize(new { value, threshold = _options.ThresholdValue });

        _db.SlaEvaluations.Add(new SlaEvaluation(
            id: evaluationId,
            companyId: _options.TenantId,
            contractVersionId: _options.ContractVersionId,
            slaRuleVersionId: _options.SlaRuleVersionId,
            evaluationResult: SlaEvaluationResult.Fail,
            calculatedValuesJson: calculatedValuesJson,
            evaluatedAt: fact.OccurredAt));

        var violationId = Guid.NewGuid();
        _db.SlaViolations.Add(new SlaViolation(
            id: violationId,
            companyId: _options.TenantId,
            contractVersionId: _options.ContractVersionId,
            slaRuleVersionId: _options.SlaRuleVersionId,
            slaEvaluationId: evaluationId,
            calculatedValuesJson: calculatedValuesJson,
            detectedAt: now));

        var claimId = Guid.NewGuid();
        _db.Claims.Add(new Claim(
            id: claimId,
            companyId: _options.TenantId,
            contractId: _options.ContractId,
            contractVersionId: _options.ContractVersionId,
            slaRuleVersionId: _options.SlaRuleVersionId,
            status: ClaimStatus.Draft,
            amount: _options.ClaimAmount,
            currency: _options.Currency,
            openedAt: now,
            closedAt: null,
            slaViolationId: violationId,
            createdByUserId: _options.UserId));

        // Penalty read model: Penalty = ClaimAmount (bootstrap formula)
        var amount = new Money(_options.ClaimAmount, new CurrencyCode(_options.Currency));
        var penaltyJsonSnapshot = JsonSerializer.Serialize(new
        {
            PenaltyType = "Fixed",
            Value = _options.ClaimAmount,
            Currency = _options.Currency,
            Rounding = "Round",
            BaseAmountSource = "Custom",
            TriggerParameter = "demo.value"
        });

        _db.Penalties.Add(new Penalty(
            claimId: claimId,
            companyId: _options.TenantId,
            amount: amount,
            calculatedAt: now,
            penaltyJsonSnapshot: penaltyJsonSnapshot,
            explanationJson: JsonSerializer.Serialize(new
            {
                reason = "bootstrap",
                factId = command.FactId,
                value,
                threshold = _options.ThresholdValue,
                claimAmount = _options.ClaimAmount
            })));

        await _db.SaveChangesAsync(ct);

        return new CreateClaimResult(ClaimId: claimId, PenaltyClaimId: claimId);
    }

    public async Task SubmitClaimAsync(SubmitClaimCommand command, CancellationToken ct)
    {
        var claim = await _db.Claims.SingleOrDefaultAsync(c => c.Id == command.ClaimId, ct);
        if (claim == null) throw new InvalidOperationException("Claim not found.");

        claim.Submit();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<GetPenaltyByClaimIdResult?> GetPenaltyByClaimIdAsync(GetPenaltyByClaimIdQuery query, CancellationToken ct)
    {
        var p = await _db.Penalties.AsNoTracking()
            .Where(x => x.ClaimId == query.ClaimId)
            .Select(x => new { x.ClaimId, x.CalculatedAmount, x.Currency })
            .SingleOrDefaultAsync(ct);

        if (p == null) return null;

        return new GetPenaltyByClaimIdResult(p.ClaimId, p.CalculatedAmount, p.Currency);
    }

    private static decimal ExtractValue(string attributesJson)
    {
        using var doc = JsonDocument.Parse(attributesJson);
        if (!doc.RootElement.TryGetProperty("value", out var v) || v.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException("Fact.AttributesJson must contain numeric 'value'.");
        }
        return v.GetDecimal();
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

