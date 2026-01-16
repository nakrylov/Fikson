namespace Fixon.Infrastructure.BackgroundJobs;

/// <summary>
/// Тип фоновой задачи (строго технический идентификатор).
/// </summary>
public static class BackgroundJobType
{
    public const string SlaRecalculation = "sla.recalculation";
    public const string CsvImport = "csv.import";
}

