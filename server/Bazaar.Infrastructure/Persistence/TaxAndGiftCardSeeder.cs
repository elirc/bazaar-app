using Bazaar.Domain.Common;
using Bazaar.Domain.GiftCards;
using Bazaar.Domain.Tax;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Persistence;

/// <summary>
/// Seeds a few region-specific tax zones (US-CA, US-OR) and a country-wide zone (DE), plus a demo
/// gift card. Region-less US/GB addresses match no zone and use the flat fallback rate, so previously
/// computed order totals are unchanged.
/// </summary>
public static class TaxAndGiftCardSeeder
{
    public static async Task SeedAsync(BazaarDbContext db, CancellationToken ct = default)
    {
        if (!await db.TaxZones.AnyAsync(ct))
        {
            var california = new TaxZone { Name = "California", Country = "US", Region = "CA", StandardRate = 0.0950m };
            california.SetCategoryRate("food", 0.0000m); // groceries are exempt

            var oregon = new TaxZone { Name = "Oregon", Country = "US", Region = "OR", StandardRate = 0.0000m };
            var germany = new TaxZone { Name = "Germany", Country = "DE", Region = null, StandardRate = 0.1900m };
            germany.SetCategoryRate("food", 0.0700m);

            db.TaxZones.AddRange(california, oregon, germany);
        }

        if (!await db.GiftCards.AnyAsync(ct))
        {
            db.GiftCards.Add(new GiftCard
            {
                Code = "GIFT25",
                InitialBalance = new Money(25m, Money.DefaultCurrency),
                Balance = new Money(25m, Money.DefaultCurrency),
                IsActive = true,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
