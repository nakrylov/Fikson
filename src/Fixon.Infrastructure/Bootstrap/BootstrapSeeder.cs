using Fixon.Application.Bootstrap;
using Fixon.Domain.Companies;
using Fixon.Domain.Contracts;
using Fixon.Domain.Facts;
using Fixon.Domain.Users;
using Fixon.Domain.Sla;
using Fixon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fixon.Infrastructure.Bootstrap;

public interface IBootstrapSeeder
{
    Task EnsureSeededAsync(CancellationToken ct);
}

/// <summary>
/// Idempotent seeder for strict vertical slice:
/// one tenant, one contract, one contract version, one SLA rule version.
/// </summary>
public sealed class BootstrapSeeder : IBootstrapSeeder
{
    private readonly FixonDbContext _db;
    private readonly BootstrapOptions _options;

    public BootstrapSeeder(FixonDbContext db, BootstrapOptions options)
    {
        _db = db;
        _options = options;
    }

    public async Task EnsureSeededAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureFactTypeDefinitionsAsync(now, ct);

        // Company (tenant)
        var company = await _db.Companies.SingleOrDefaultAsync(x => x.Id == _options.TenantId, ct);
        if (company == null)
        {
            _db.Companies.Add(new Company(_options.TenantId, "BOOTSTRAP_TENANT", now, isActive: true));
            await _db.SaveChangesAsync(ct);
        }

        // Counterparty
        var counterparty = await _db.Counterparties.SingleOrDefaultAsync(x => x.Id == _options.CounterpartyId, ct);
        if (counterparty == null)
        {
            _db.Counterparties.Add(new Counterparty(_options.CounterpartyId, _options.TenantId, "BOOTSTRAP_COUNTERPARTY", null, isActive: true));
            await _db.SaveChangesAsync(ct);
        }

        // User (system-ish)
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == _options.UserId, ct);
        if (user == null)
        {
            _db.Users.Add(new User(_options.UserId, _options.TenantId, "bootstrap@local", "Bootstrap", "not-used", now, isActive: true));
            await _db.SaveChangesAsync(ct);
        }

        // Contract
        var contract = await _db.Contracts.SingleOrDefaultAsync(x => x.Id == _options.ContractId, ct);
        if (contract == null)
        {
            _db.Contracts.Add(new Contract(_options.ContractId, _options.TenantId, _options.CounterpartyId, "BOOTSTRAP_CONTRACT", now, isActive: true));
            await _db.SaveChangesAsync(ct);
        }

        // ContractVersion
        var version = await _db.ContractVersions.SingleOrDefaultAsync(x => x.Id == _options.ContractVersionId, ct);
        if (version == null)
        {
            var v = new ContractVersion(
                id: _options.ContractVersionId,
                companyId: _options.TenantId,
                contractId: _options.ContractId,
                versionNumber: 1,
                effectiveFrom: now,
                effectiveTo: null,
                pdfFilePath: null,
                createdAt: now,
                createdByUserId: _options.UserId,
                isActive: true);

            v.SetSnapshots("{}", "{}");
            v.Sign(now);

            _db.ContractVersions.Add(v);
            await _db.SaveChangesAsync(ct);
        }

        // SLA rule + version (we store threshold in ConditionJson for traceability)
        var rule = await _db.SlaRules.SingleOrDefaultAsync(x => x.Id == _options.SlaRuleId, ct);
        if (rule == null)
        {
            _db.SlaRules.Add(new SlaRule(_options.SlaRuleId, _options.TenantId, _options.ContractId, "BOOTSTRAP_RULE", now, isActive: true));
            await _db.SaveChangesAsync(ct);
        }

        var ruleVersion = await _db.SlaRuleVersions.SingleOrDefaultAsync(x => x.Id == _options.SlaRuleVersionId, ct);
        if (ruleVersion == null)
        {
            var appliesWhen = "{}";
            var condition = System.Text.Json.JsonSerializer.Serialize(new { factType = "demo.value", threshold = _options.ThresholdValue });
            var penaltyJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                PenaltyType = "Fixed",
                Value = _options.ClaimAmount,
                Currency = _options.Currency,
                Rounding = "Round",
                BaseAmountSource = "Custom",
                TriggerParameter = "demo.value",
                Threshold = (decimal?)null,
                MinPenalty = (decimal?)null,
                MaxPenalty = (decimal?)null
            });

            _db.SlaRuleVersions.Add(new SlaRuleVersion(
                id: _options.SlaRuleVersionId,
                companyId: _options.TenantId,
                slaRuleId: _options.SlaRuleId,
                versionNumber: 1,
                appliesWhenJson: appliesWhen,
                conditionJson: condition,
                penaltyJson: penaltyJson,
                effectiveFrom: now,
                effectiveTo: null,
                createdAt: now,
                createdByUserId: _options.UserId,
                isActive: true));

            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureFactTypeDefinitionsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var existingEventTypes = await _db.FactTypeDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => x.EventType)
            .ToListAsync(ct);

        var existingSet = new HashSet<string>(existingEventTypes, StringComparer.OrdinalIgnoreCase);
        var seedRows = new[]
        {
            new FactTypeDefinition(
                id: Guid.NewGuid(),
                eventType: "TEMPERATURE_READING",
                displayName: "Temperature reading",
                valueType: FactTypeDefinition.ValueTypeNumber,
                defaultConditionType: FactTypeDefinition.ConditionTypeRange,
                unit: "°C",
                isActive: true,
                createdAt: now),
            new FactTypeDefinition(
                id: Guid.NewGuid(),
                eventType: "DELIVERY_DELAY",
                displayName: "Delivery delay",
                valueType: FactTypeDefinition.ValueTypeNumber,
                defaultConditionType: FactTypeDefinition.ConditionTypeThreshold,
                unit: null,
                isActive: true,
                createdAt: now),
            new FactTypeDefinition(
                id: Guid.NewGuid(),
                eventType: "DOCUMENT_MISSING",
                displayName: "Document missing",
                valueType: FactTypeDefinition.ValueTypeBoolean,
                defaultConditionType: FactTypeDefinition.ConditionTypeBoolean,
                unit: null,
                isActive: true,
                createdAt: now),
            new FactTypeDefinition(
                id: Guid.NewGuid(),
                eventType: "DELIVERY_PLANNED",
                displayName: "Delivery planned",
                valueType: FactTypeDefinition.ValueTypeDatetime,
                defaultConditionType: FactTypeDefinition.ConditionTypeThreshold,
                unit: null,
                isActive: true,
                createdAt: now),
            new FactTypeDefinition(
                id: Guid.NewGuid(),
                eventType: "DELIVERY_ACTUAL",
                displayName: "Delivery actual",
                valueType: FactTypeDefinition.ValueTypeDatetime,
                defaultConditionType: FactTypeDefinition.ConditionTypeThreshold,
                unit: null,
                isActive: true,
                createdAt: now)
        };

        var newRows = seedRows
            .Where(x => !existingSet.Contains(x.EventType))
            .ToList();
        if (newRows.Count == 0)
        {
            return;
        }

        _db.FactTypeDefinitions.AddRange(newRows);
        await _db.SaveChangesAsync(ct);
    }
}

