using Bazaar.Domain.Checkout;
using Bazaar.Domain.Common;
using Bazaar.Domain.Tax;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Tax;

public interface ITaxService
{
    Task<Money> CalculateTaxAsync(
        Address address,
        IReadOnlyList<(decimal Amount, string Category)> lines,
        string currency,
        CancellationToken ct = default);
}

/// <summary>
/// Computes tax by matching the buyer's address to a <see cref="TaxZone"/> (region-specific first,
/// then country-wide) and applying the zone's per-category rate to each line. When no zone matches,
/// the legacy flat rate is used so pre-existing order totals are preserved.
/// </summary>
public sealed class ZoneTaxService : ITaxService
{
    public const decimal FallbackRate = FlatRateTaxCalculator.Rate;

    private readonly BazaarDbContext _db;

    public ZoneTaxService(BazaarDbContext db) => _db = db;

    public async Task<Money> CalculateTaxAsync(
        Address address,
        IReadOnlyList<(decimal Amount, string Category)> lines,
        string currency,
        CancellationToken ct = default)
    {
        var country = address.Country;
        var zones = await _db.TaxZones.AsNoTracking()
            .Include(z => z.CategoryRates)
            .Where(z => z.Country == country)
            .ToListAsync(ct);

        // Prefer an exact region match; otherwise a country-wide (region-less) zone.
        var zone =
            zones.FirstOrDefault(z => z.Region != null
                && string.Equals(z.Region, address.Region, StringComparison.OrdinalIgnoreCase))
            ?? zones.FirstOrDefault(z => z.Region == null);

        var tax = 0m;
        foreach (var (amount, category) in lines)
        {
            var rate = zone?.RateFor(string.IsNullOrWhiteSpace(category) ? TaxZone.DefaultCategory : category)
                       ?? FallbackRate;
            tax += amount * rate;
        }

        return new Money(tax, currency);
    }
}
