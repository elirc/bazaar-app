using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;

namespace Bazaar.Api.Contracts;

public static class CatalogMappings
{
    public static MoneyDto ToDto(this Money money) => new(money.Amount, money.Currency);

    public static VariantDto ToDto(this ProductVariant variant, int available) => new(
        variant.Id,
        variant.Sku,
        variant.Title,
        variant.Price.ToDto(),
        variant.Position,
        variant.Options.Select(o => new VariantOptionDto(o.Name, o.Value)).ToList(),
        available);

    public static ProductDetailDto ToDetailDto(this Product product, IReadOnlyDictionary<Guid, int> stockByVariant) => new(
        product.Id,
        product.Slug,
        product.Title,
        product.Description,
        product.Vendor,
        product.Status.ToString(),
        product.Images
            .OrderBy(i => i.Position)
            .Select(i => new ProductImageDto(i.Id, i.Url, i.AltText, i.Position))
            .ToList(),
        product.Variants
            .OrderBy(v => v.Position)
            .Select(v => v.ToDto(stockByVariant.TryGetValue(v.Id, out var qty) ? qty : 0))
            .ToList(),
        product.Collections.Select(c => c.Slug).OrderBy(s => s).ToList());

    public static CollectionDto ToDto(this Collection collection, int productCount) =>
        new(collection.Id, collection.Slug, collection.Title, collection.Description, productCount);
}
