namespace Bazaar.Domain.Catalog;

/// <summary>An image associated with a product, stored as a URL.</summary>
public class ProductImage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int Position { get; set; }
}
