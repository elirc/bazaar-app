namespace Bazaar.Domain.Catalog;

/// <summary>A curated grouping of products (e.g. "Summer Sale").</summary>
public class Collection
{
    private readonly List<Product> _products = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<Product> Products => _products;
}
