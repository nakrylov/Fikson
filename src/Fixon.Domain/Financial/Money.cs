using System.Globalization;

namespace Fixon.Domain.Financial;

/// <summary>
/// Денежная сумма: Amount + Currency.
/// Fixon считает последствия, но не является бухгалтерией/биллингом (см. product vision).
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    public Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public override string ToString()
        => $"{Amount.ToString(CultureInfo.InvariantCulture)} {Currency.Value}";

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency.Value, other.Currency.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Currency mismatch: {Currency.Value} vs {other.Currency.Value}");
        }
    }
}

