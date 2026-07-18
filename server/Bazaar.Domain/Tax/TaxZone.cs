namespace Bazaar.Domain.Tax;

/// <summary>
/// A tax jurisdiction matched by country and optional region. Carries a standard rate plus optional
/// per-category overrides. When no zone matches a buyer's address, checkout falls back to a default rate.
/// </summary>
public class TaxZone
{
    public const string DefaultCategory = "standard";

    private readonly List<TaxCategoryRate> _categoryRates = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Region/state code, or null for a country-wide zone.</summary>
    public string? Region { get; set; }

    /// <summary>Rate applied to the "standard" category and any category without an explicit override.</summary>
    public decimal StandardRate { get; set; }

    public IReadOnlyList<TaxCategoryRate> CategoryRates => _categoryRates;

    public TaxCategoryRate SetCategoryRate(string category, decimal rate)
    {
        var existing = _categoryRates.FirstOrDefault(r =>
            string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Rate = rate;
            return existing;
        }
        var added = new TaxCategoryRate { Category = category, Rate = rate };
        _categoryRates.Add(added);
        return added;
    }

    /// <summary>The rate for a product tax category: an explicit override, else the standard rate.</summary>
    public decimal RateFor(string category)
    {
        var match = _categoryRates.FirstOrDefault(r =>
            string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase));
        return match?.Rate ?? StandardRate;
    }
}

/// <summary>A per-category tax rate override within a <see cref="TaxZone"/>.</summary>
public class TaxCategoryRate
{
    public string Category { get; set; } = TaxZone.DefaultCategory;
    public decimal Rate { get; set; }
}
