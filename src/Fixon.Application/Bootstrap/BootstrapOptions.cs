namespace Fixon.Application.Bootstrap;

public sealed class BootstrapOptions
{
    public bool Enabled { get; set; }

    public Guid TenantId { get; set; }
    public Guid CounterpartyId { get; set; }
    public Guid UserId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ContractVersionId { get; set; }
    public Guid SlaRuleId { get; set; }
    public Guid SlaRuleVersionId { get; set; }

    public decimal ThresholdValue { get; set; } = 10m;

    /// <summary>
    /// Fixed v1 formula for bootstrap: ClaimAmount and Penalty amount.
    /// </summary>
    public decimal ClaimAmount { get; set; } = 100m;

    public string Currency { get; set; } = "RUB";
}

