using Bazaar.Domain.Common;

namespace Bazaar.Domain.Checkout;

public interface ITaxCalculator
{
    Money CalculateTax(Money taxableSubtotal);
}

public interface IShippingCalculator
{
    Money CalculateShipping(Money subtotal, int itemCount);
}

/// <summary>Applies a single flat tax rate to the taxable subtotal.</summary>
public sealed class FlatRateTaxCalculator : ITaxCalculator
{
    public const decimal Rate = 0.0825m; // 8.25%

    public Money CalculateTax(Money taxableSubtotal) => taxableSubtotal.MultiplyByRate(Rate);
}

/// <summary>Free shipping over a threshold; otherwise a flat fee. Free for empty orders.</summary>
public sealed class ThresholdShippingCalculator : IShippingCalculator
{
    public const decimal FreeShippingThreshold = 75.00m;
    public const decimal FlatFee = 5.99m;

    public Money CalculateShipping(Money subtotal, int itemCount)
    {
        if (itemCount <= 0 || subtotal.Amount >= FreeShippingThreshold)
            return Money.Zero(subtotal.Currency);
        return new Money(FlatFee, subtotal.Currency);
    }
}
