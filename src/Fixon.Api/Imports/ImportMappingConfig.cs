namespace Fixon.Api.Imports;

public static class ImportMappingConfig
{
    public const string PlannedDeliveryTimeColumn = "plannedDeliveryTime";
    public const string ActualDeliveryTimeColumn = "actualDeliveryTime";
    public const string TemperatureColumn = "temperature";
    public const string DocumentsMissingColumn = "documentsMissing";

    public static readonly IReadOnlyDictionary<string, string> TabularColumnToEventType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TemperatureColumn] = "TEMPERATURE_READING",
            [DocumentsMissingColumn] = "DOCUMENT_MISSING",
            [PlannedDeliveryTimeColumn] = "DELIVERY_PLANNED",
            [ActualDeliveryTimeColumn] = "DELIVERY_ACTUAL"
        };

    public static readonly IReadOnlyCollection<string> TabularHintColumns =
    [
        PlannedDeliveryTimeColumn,
        ActualDeliveryTimeColumn,
        TemperatureColumn,
        DocumentsMissingColumn
    ];
}
