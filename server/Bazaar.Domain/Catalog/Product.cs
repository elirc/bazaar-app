namespace Bazaar.Domain.Catalog;

/// <summary>A sellable product. Aggregate root over its variants and images.</summary>
public class Product
{
    private readonly List<ProductVariant> _variants = new();
    private readonly List<ProductImage> _images = new();
    private readonly List<Collection> _collections = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public ProductStatus Status { get; set; } = ProductStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<ProductVariant> Variants => _variants;
    public IReadOnlyList<ProductImage> Images => _images;
    public IReadOnlyList<Collection> Collections => _collections;

    public ProductVariant AddVariant(ProductVariant variant)
    {
        variant.ProductId = Id;
        _variants.Add(variant);
        return variant;
    }

    public ProductImage AddImage(ProductImage image)
    {
        image.ProductId = Id;
        _images.Add(image);
        return image;
    }

    public void AddToCollection(Collection collection)
    {
        if (!_collections.Contains(collection))
            _collections.Add(collection);
    }

    public void ClearImages() => _images.Clear();

    public void SetCollections(IEnumerable<Collection> collections)
    {
        _collections.Clear();
        foreach (var collection in collections)
            _collections.Add(collection);
    }
}
