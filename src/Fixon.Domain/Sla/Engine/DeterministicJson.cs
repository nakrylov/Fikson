using System.Text.Json;

namespace Fixon.Domain.Sla.Engine;

/// <summary>
/// Детерминированная сериализация calculated values для аудитируемости:
/// - ключи сортируются
/// - формат стабильный (без pretty print)
/// </summary>
public static class DeterministicJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public static string SerializeSorted(IDictionary<string, object?> values)
    {
        // Сортируем ключи для детерминизма.
        var sorted = new SortedDictionary<string, object?>(values, StringComparer.Ordinal);
        return JsonSerializer.Serialize(sorted, Options);
    }
}

