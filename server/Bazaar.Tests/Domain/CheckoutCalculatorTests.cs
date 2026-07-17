using Bazaar.Domain.Checkout;
using Bazaar.Domain.Common;
using Bazaar.Domain.Payments;
using Bazaar.Infrastructure.Payments;

namespace Bazaar.Tests.Domain;

public class CheckoutCalculatorTests
{
    private static Money Usd(decimal amount) => new(amount, "USD");

    [Theory]
    [InlineData(100.00, 8.25)]
    [InlineData(28.00, 2.31)]
    [InlineData(89.00, 7.34)] // 7.3425 rounds to 7.34
    public void Tax_applies_the_flat_rate(decimal subtotal, decimal expectedTax)
    {
        var tax = new FlatRateTaxCalculator().CalculateTax(Usd(subtotal));
        Assert.Equal(Usd(expectedTax), tax);
    }

    [Fact]
    public void Shipping_is_flat_below_the_threshold()
    {
        var shipping = new ThresholdShippingCalculator().CalculateShipping(Usd(28.00m), itemCount: 2);
        Assert.Equal(Usd(5.99m), shipping);
    }

    [Fact]
    public void Shipping_is_free_at_or_above_the_threshold()
    {
        var shipping = new ThresholdShippingCalculator().CalculateShipping(Usd(75.00m), itemCount: 1);
        Assert.Equal(Money.Zero("USD"), shipping);
    }

    [Fact]
    public void Shipping_is_free_for_an_empty_order()
    {
        var shipping = new ThresholdShippingCalculator().CalculateShipping(Money.Zero("USD"), itemCount: 0);
        Assert.Equal(Money.Zero("USD"), shipping);
    }

    [Fact]
    public async Task Fake_gateway_approves_a_normal_charge()
    {
        var result = await new FakePaymentGateway()
            .ChargeAsync(new PaymentRequest("BZ-1001", Usd(36.30m), "buyer@example.com"));
        Assert.True(result.Succeeded);
        Assert.NotNull(result.TransactionId);
    }

    [Fact]
    public async Task Fake_gateway_declines_for_a_decline_email()
    {
        var result = await new FakePaymentGateway()
            .ChargeAsync(new PaymentRequest("BZ-1001", Usd(36.30m), "decline@example.com"));
        Assert.False(result.Succeeded);
        Assert.NotNull(result.FailureReason);
    }
}
