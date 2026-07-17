namespace Bazaar.Domain.Common;

/// <summary>
/// A monetary amount in a specific currency. Immutable value object.
/// Amounts are rounded to 2 decimal places (banker's rounding) on construction,
/// and persisted as integer minor units (cents) to avoid SQLite decimal drift.
/// </summary>
public sealed record Money
{
    public const string DefaultCurrency = "USD";

    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money FromCents(long cents, string currency = DefaultCurrency) =>
        new(cents / 100m, currency);

    /// <summary>Value as integer minor units (cents), rounded with banker's rounding.</summary>
    public long ToCents() => (long)decimal.Round(Amount * 100m, 0, MidpointRounding.ToEven);

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

    public Money MultiplyBy(int quantity) => new(Amount * quantity, Currency);

    /// <summary>Multiply by a rate (e.g. a tax rate) and round the result to cents.</summary>
    public Money MultiplyByRate(decimal rate) => new(Amount * rate, Currency);

    public bool IsNegative => Amount < 0m;
    public bool IsZero => Amount == 0m;

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);
    public static Money operator *(Money a, int quantity) => a.MultiplyBy(quantity);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot operate on money of differing currencies: {Currency} vs {other.Currency}.");
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
