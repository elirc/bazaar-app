using Bazaar.Domain.Common;

namespace Bazaar.Domain.Catalog;

/// <summary>A purchasable variant of a product (a specific SKU with its own price and options).</summary>
public class ProductVariant
{
    private readonly List<VariantOption> _options = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Title { get; set; } = "Default";
    public Money Price { get; set; } = Money.Zero();
    public int Position { get; set; }

    public IReadOnlyList<VariantOption> Options => _options;

    public Product? Product { get; set; }

    public VariantOption SetOption(string name, string value)
    {
        var existing = _options.FirstOrDefault(o =>
            string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Value = value;
            return existing;
        }

        var option = new VariantOption { Name = name, Value = value };
        _options.Add(option);
        return option;
    }
}

/// <summary>A single selected option on a variant, e.g. Size = Small.</summary>
public class VariantOption
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
