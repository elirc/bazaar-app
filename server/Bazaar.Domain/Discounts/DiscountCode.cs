using Bazaar.Domain.Common;

namespace Bazaar.Domain.Discounts;

/// <summary>A redeemable discount: a percentage off, or a fixed amount off, with optional expiry and usage cap.</summary>
public class DiscountCode
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public DiscountType Type { get; set; }

    /// <summary>Percentage (0-100) when <see cref="Type"/> is Percentage; otherwise the fixed amount off.</summary>
    public decimal Value { get; set; }

    /// <summary>Currency for a fixed-amount discount. Ignored for percentage discounts.</summary>
    public string Currency { get; set; } = Money.DefaultCurrency;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int TimesUsed { get; set; }

    public bool IsRedeemable(DateTimeOffset now)
    {
        if (!IsActive) return false;
        if (StartsAt is { } start && now < start) return false;
        if (EndsAt is { } end && now > end) return false;
        if (UsageLimit is { } limit && TimesUsed >= limit) return false;
        return true;
    }

    /// <summary>Compute the discount amount for a given subtotal, capped so it never exceeds the subtotal.</summary>
    public Money ComputeDiscount(Money subtotal)
    {
        var raw = Type == DiscountType.Percentage
            ? subtotal.MultiplyByRate(Value / 100m)
            : new Money(Value, subtotal.Currency);

        if (raw.Amount > subtotal.Amount)
            return subtotal;
        return raw.IsNegative ? Money.Zero(subtotal.Currency) : raw;
    }

    public void MarkRedeemed() => TimesUsed++;
}
