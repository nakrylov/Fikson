namespace Fixon.Domain.Abstractions;

public abstract class ActivatableTenantEntity : TenantEntity
{
    public bool IsActive { get; protected set; } = true;
}


