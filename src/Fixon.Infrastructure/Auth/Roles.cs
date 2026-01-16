namespace Fixon.Infrastructure.Auth;

/// <summary>
/// Роли пользователей системы (см. требования к ролевой модели).
/// 
/// ВАЖНО: Роли не используются как флаги бизнес-логики в Domain.
/// Domain не знает про роли — они используются только в Application/API слоях.
/// </summary>
public static class Roles
{
    /// <summary>
    /// Поклажедатель — основной пользователь системы.
    /// Полный доступ к своим договорам, SLA, отчётам.
    /// </summary>
    public const string Pledgor = "Pledgor";

    /// <summary>
    /// 3PL оператор — ограниченный доступ.
    /// Может загружать данные, подтверждать факты.
    /// Не видит финансовые условия и агрегированные SLA другого tenant'а.
    /// </summary>
    public const string ThreePL = "ThreePL";

    /// <summary>
    /// System Admin — служебный доступ.
    /// Bypass tenant-фильтров.
    /// Используется для администрирования, фоновых задач, support-операций.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Все роли системы.
    /// </summary>
    public static readonly string[] All = { Pledgor, ThreePL, Admin };
}

