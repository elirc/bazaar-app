using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Domain.Shipping;

namespace Bazaar.Tests.Domain;

public class ShippingMethodTests
{
    private static Money Usd(decimal amount) => new(amount, "USD");

    [Fact]
    public void Flat_method_charges_the_base_rate()
    {
        var method = new ShippingMethod { RateType = ShippingRateType.Flat, BaseRate = Usd(14.99m) };
        Assert.Equal(Usd(14.99m), method.CalculateCost(Usd(20m), itemCount: 1, totalWeightGrams: 5000));
    }

    [Fact]
    public void Weight_method_adds_a_per_kilogram_surcharge()
    {
        var method = new ShippingMethod
        {
            RateType = ShippingRateType.Weight,
            BaseRate = Usd(3.99m),
            PerKgRate = 1.50m,
        };
        // 3.99 + 1.50 * 1.5kg = 6.24
        Assert.Equal(Usd(6.24m), method.CalculateCost(Usd(50m), itemCount: 1, totalWeightGrams: 1500));
    }

    [Theory]
    [InlineData(74.99, 5.99)]
    [InlineData(75.00, 0.00)]
    [InlineData(120.00, 0.00)]
    public void FreeOverThreshold_method_ships_free_at_or_above_the_threshold(decimal subtotal, decimal expected)
    {
        var method = new ShippingMethod
        {
            RateType = ShippingRateType.FreeOverThreshold,
            BaseRate = Usd(5.99m),
            FreeThreshold = 75.00m,
        };
        Assert.Equal(Usd(expected), method.CalculateCost(Usd(subtotal), itemCount: 2, totalWeightGrams: 0));
    }

    [Fact]
    public void An_empty_cart_always_ships_free()
    {
        var method = new ShippingMethod { RateType = ShippingRateType.Flat, BaseRate = Usd(14.99m) };
        Assert.Equal(Money.Zero("USD"), method.CalculateCost(Money.Zero("USD"), itemCount: 0, totalWeightGrams: 0));
    }
}
