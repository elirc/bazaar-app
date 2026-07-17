using Bazaar.Domain.Common;

namespace Bazaar.Tests.Domain;

public class MoneyTests
{
    [Theory]
    [InlineData(19.99, 19.99)]
    [InlineData(19.994, 19.99)]
    [InlineData(19.996, 20.00)]
    [InlineData(10.125, 10.12)] // banker's rounding: 2 is even
    [InlineData(10.135, 10.14)] // banker's rounding: rounds to even 4
    public void Constructor_rounds_to_two_places_using_bankers_rounding(decimal input, decimal expected)
    {
        var money = new Money(input, "USD");
        Assert.Equal(expected, money.Amount);
    }

    [Fact]
    public void ToCents_and_FromCents_round_trip()
    {
        var money = new Money(19.99m, "USD");
        Assert.Equal(1999, money.ToCents());
        Assert.Equal(money, Money.FromCents(1999, "USD"));
    }

    [Fact]
    public void Currency_is_normalised_to_uppercase()
    {
        Assert.Equal("USD", new Money(1m, "usd").Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("DOLLARS")]
    public void Constructor_rejects_invalid_currency(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(1m, currency));
    }

    [Fact]
    public void Add_and_Subtract_operate_within_the_same_currency()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(2.50m, "USD");
        Assert.Equal(new Money(12.50m, "USD"), a + b);
        Assert.Equal(new Money(7.50m, "USD"), a - b);
    }

    [Fact]
    public void MultiplyBy_scales_by_an_integer_quantity()
    {
        Assert.Equal(new Money(59.97m, "USD"), new Money(19.99m, "USD").MultiplyBy(3));
    }

    [Fact]
    public void MultiplyByRate_applies_a_tax_rate_and_rounds()
    {
        // 8.25% tax on $100.00 -> $8.25
        Assert.Equal(new Money(8.25m, "USD"), new Money(100.00m, "USD").MultiplyByRate(0.0825m));
        // rounding: 7% of $19.99 = 1.3993 -> 1.40
        Assert.Equal(new Money(1.40m, "USD"), new Money(19.99m, "USD").MultiplyByRate(0.07m));
    }

    [Fact]
    public void Operating_across_currencies_throws()
    {
        var usd = new Money(10m, "USD");
        var eur = new Money(10m, "EUR");
        Assert.Throws<InvalidOperationException>(() => usd + eur);
    }

    [Fact]
    public void Value_equality_holds_for_equal_amount_and_currency()
    {
        Assert.Equal(new Money(5m, "USD"), new Money(5m, "USD"));
        Assert.NotEqual(new Money(5m, "USD"), new Money(5m, "EUR"));
    }
}
