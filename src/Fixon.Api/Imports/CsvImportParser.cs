using Microsoft.VisualBasic.FileIO;

namespace Fixon.Api.Imports;

internal static class CsvImportParser
{
    public static IReadOnlyList<Dictionary<string, string>> Parse(string csv, string delimiter = ",")
    {
        using var reader = new StringReader(csv);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(delimiter);

        if (parser.EndOfData) return Array.Empty<Dictionary<string, string>>();

        var header = parser.ReadFields();
        if (header == null || header.Length == 0)
        {
            throw new FormatException("CSV header is missing.");
        }

        var result = new List<Dictionary<string, string>>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? Array.Empty<string>();
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Length; i++)
            {
                var key = header[i];
                if (string.IsNullOrWhiteSpace(key)) continue;
                row[key] = i < fields.Length ? fields[i] : string.Empty;
            }
            result.Add(row);
        }

        return result;
    }
}

