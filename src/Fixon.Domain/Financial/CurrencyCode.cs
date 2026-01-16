using System.Text.RegularExpressions;

namespace Fixon.Domain.Financial;

/// <summary>
/// Валютный код (ISO 4217).
/// Храним как string, но валидируем, чтобы не было "руб"/"RUB " и т.п.
/// </summary>
public readonly record struct CurrencyCode
{
    private static readonly Regex Iso4217 = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public string Value { get; }

    public CurrencyCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Currency code is required.", nameof(value));
        }

        value = value.Trim().ToUpperInvariant();

        if (!Iso4217.IsMatch(value))
        {
            throw new ArgumentException("Currency code must be ISO 4217 (e.g. RUB, USD).", nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}

