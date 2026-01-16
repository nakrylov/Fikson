using System.Diagnostics.Metrics;

namespace Fixon.Api.Observability;

public static class ImportMetrics
{
    private static readonly Meter Meter = new("Fixon.Imports", "1.0.0");

    public static readonly Counter<long> RowsUploaded = Meter.CreateCounter<long>("fixon.import.rows_uploaded");
    public static readonly Counter<long> RowsValidated = Meter.CreateCounter<long>("fixon.import.rows_validated");
    public static readonly Counter<long> RowsInvalid = Meter.CreateCounter<long>("fixon.import.rows_invalid");
    public static readonly Counter<long> RowsDuplicate = Meter.CreateCounter<long>("fixon.import.rows_duplicate");
    public static readonly Counter<long> FactsCommitted = Meter.CreateCounter<long>("fixon.import.facts_committed");
}

