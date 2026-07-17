using Bazaar.Domain.Catalog;

namespace Bazaar.Domain.Carts;

/// <summary>A line in a cart: a variant plus a quantity. Price is resolved from the variant at read time.</summary>
public class CartLineItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CartId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }

    public ProductVariant? Variant { get; set; }
}
