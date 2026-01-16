namespace Fixon.Domain.Abstractions;

/// <summary>
/// Маркерный интерфейс для tenant-scoped сущностей.
/// Используется EF Core для автоматического применения query filters.
/// </summary>
public interface ITenantEntity
{
    Guid CompanyId { get; }
}

/// <summary>
/// Базовый класс для tenant-scoped сущностей.
/// Все такие сущности содержат CompanyId и фильтруются по нему.
/// </summary>
public abstract class TenantEntity : Entity, ITenantEntity
{
    public Guid CompanyId { get; protected set; }
}


