using Bazaar.Domain.Common;
using Bazaar.Domain.GiftCards;
using Bazaar.Domain.Tax;

namespace Bazaar.Tests.Domain;

public class TaxAndGiftCardTests
{
    private static Money Usd(decimal amount) => new(amount, "USD");

    [Fact]
    public void Tax_zone_uses_a_category_override_before_the_standard_rate()
    {
        var zone = new TaxZone { Country = "US", Region = "CA", StandardRate = 0.095m };
        zone.SetCategoryRate("food", 0m);

        Assert.Equal(0.095m, zone.RateFor("standard"));
        Assert.Equal(0.095m, zone.RateFor("apparel"));   // no override -> standard
        Assert.Equal(0m, zone.RateFor("food"));          // override
    }

    [Fact]
    public void Gift_card_applies_the_lesser_of_its_balance_and_the_total()
    {
        var card = new GiftCard { Code = "GC", Balance = Usd(25m), InitialBalance = Usd(25m), IsActive = true };

        Assert.Equal(Usd(25m), card.AmountToApply(Usd(40m))); // total exceeds balance -> whole balance
        Assert.Equal(Usd(10m), card.AmountToApply(Usd(10m))); // balance exceeds total -> whole total
    }

    [Fact]
    public void Redeeming_reduces_the_balance_and_never_goes_negative()
    {
        var card = new GiftCard { Code = "GC", Balance = Usd(25m), InitialBalance = Usd(25m), IsActive = true };
        card.Redeem(Usd(10m));
        Assert.Equal(Usd(15m), card.Balance);

        card.Redeem(Usd(100m));
        Assert.Equal(Usd(0m), card.Balance);
        Assert.False(card.IsRedeemable);
    }
}
