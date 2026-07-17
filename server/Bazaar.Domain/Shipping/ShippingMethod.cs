using Bazaar.Domain.Common;

namespace Bazaar.Domain.Shipping;

/// <summary>
/// A selectable shipping option with a pricing rule (flat, weight-based, or free-over-threshold)
/// and a delivery-time estimate. Replaces the earlier single fixed shipping calculator.
/// </summary>
public class ShippingMethod
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ShippingRateType RateType { get; set; }

    /// <summary>Flat fee, or the base fee for a weight-based / threshold method.</summary>
    public Money BaseRate { get; set; } = Money.Zero();

    /// <summary>Per-kilogram surcharge for <see cref="ShippingRateType.Weight"/>.</summary>
    public decimal PerKgRate { get; set; }

    /// <summary>Subtotal at or above which <see cref="ShippingRateType.FreeOverThreshold"/> ships free.</summary>
    public decimal? FreeThreshold { get; set; }

    public int MinDays { get; set; }
    public int MaxDays { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }

    public string DeliveryEstimate =>
        MinDays == MaxDays ? $"{MinDays} business days" : $"{MinDays}–{MaxDays} business days";

    /// <summary>Compute the shipping charge for a cart. Empty carts always ship free.</summary>
    public Money CalculateCost(Money subtotal, int itemCount, int totalWeightGrams)
    {
        var currency = subtotal.Currency;
        if (itemCount <= 0)
            return Money.Zero(currency);

        return RateType switch
        {
            ShippingRateType.Flat => new Money(BaseRate.Amount, currency),
            ShippingRateType.Weight => new Money(
                BaseRate.Amount + PerKgRate * (totalWeightGrams / 1000m), currency),
            ShippingRateType.FreeOverThreshold =>
                FreeThreshold is { } threshold && subtotal.Amount >= threshold
                    ? Money.Zero(currency)
                    : new Money(BaseRate.Amount, currency),
            _ => new Money(BaseRate.Amount, currency),
        };
    }
}
