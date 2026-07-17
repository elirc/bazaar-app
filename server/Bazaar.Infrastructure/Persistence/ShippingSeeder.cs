using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Domain.Shipping;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Persistence;

/// <summary>
/// Seeds the shipping methods. The default "Standard" method reproduces the legacy fixed
/// calculator (flat $5.99, free at/above $75) so pre-existing order totals are preserved.
/// </summary>
public static class ShippingSeeder
{
    public static async Task SeedAsync(BazaarDbContext db, CancellationToken ct = default)
    {
        if (await db.ShippingMethods.AnyAsync(ct))
            return;

        db.ShippingMethods.AddRange(
            new ShippingMethod
            {
                Code = "standard",
                Name = "Standard",
                RateType = ShippingRateType.FreeOverThreshold,
                BaseRate = new Money(5.99m, Money.DefaultCurrency),
                FreeThreshold = 75.00m,
                MinDays = 3,
                MaxDays = 5,
                IsActive = true,
                IsDefault = true,
                DisplayOrder = 0,
            },
            new ShippingMethod
            {
                Code = "express",
                Name = "Express",
                RateType = ShippingRateType.Flat,
                BaseRate = new Money(14.99m, Money.DefaultCurrency),
                MinDays = 1,
                MaxDays = 2,
                IsActive = true,
                DisplayOrder = 1,
            },
            new ShippingMethod
            {
                Code = "freight",
                Name = "Freight (by weight)",
                RateType = ShippingRateType.Weight,
                BaseRate = new Money(3.99m, Money.DefaultCurrency),
                PerKgRate = 1.50m,
                MinDays = 5,
                MaxDays = 8,
                IsActive = true,
                DisplayOrder = 2,
            });

        await db.SaveChangesAsync(ct);
    }
}
