using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

// ---- Response DTOs ----

public sealed record ProductSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string? Vendor,
    string Status,
    string? ImageUrl,
    MoneyDto? PriceFrom,
    IReadOnlyList<string> Collections);

public sealed record VariantDto(
    Guid Id,
    string Sku,
    string Title,
    MoneyDto Price,
    int Position,
    IReadOnlyList<VariantOptionDto> Options,
    int Available);

public sealed record VariantOptionDto(string Name, string Value);

public sealed record ProductImageDto(Guid Id, string Url, string? AltText, int Position);

public sealed record ProductDetailDto(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string? Vendor,
    string Status,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<VariantDto> Variants,
    IReadOnlyList<string> Collections);

public sealed record CollectionDto(
    Guid Id,
    string Slug,
    string Title,
    string? Description,
    int ProductCount);

// ---- Request DTOs ----
// Note: "required" fields are declared nullable so a missing value fails validation with a 400
// rather than silently binding to default(T) (the classic [Required]-on-non-nullable no-op).

public sealed record ImageInput
{
    [Required, Url, StringLength(1000)]
    public string? Url { get; init; }

    [StringLength(300)]
    public string? AltText { get; init; }

    public int Position { get; init; }
}

public sealed record VariantInput
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string? Sku { get; init; }

    [StringLength(160)]
    public string? Title { get; init; }

    [Required]
    [Range(0, 1_000_000)]
    public decimal? Price { get; init; }

    [StringLength(3, MinimumLength = 3)]
    public string? Currency { get; init; }

    [Range(0, 1_000_000)]
    public int StockOnHand { get; init; }

    public List<VariantOptionInput> Options { get; init; } = new();
}

public sealed record VariantOptionInput
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string? Name { get; init; }

    [Required, StringLength(120, MinimumLength = 1)]
    public string? Value { get; init; }
}

public sealed record CreateProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string? Title { get; init; }

    [Required, StringLength(160, MinimumLength = 1)]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug must be lowercase words separated by hyphens.")]
    public string? Slug { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }

    [StringLength(120)]
    public string? Vendor { get; init; }

    public string? Status { get; init; }

    public List<ImageInput> Images { get; init; } = new();

    [MinLength(1, ErrorMessage = "A product needs at least one variant.")]
    public List<VariantInput> Variants { get; init; } = new();

    public List<string> CollectionSlugs { get; init; } = new();
}

public sealed record UpdateProductRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string? Title { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }

    [StringLength(120)]
    public string? Vendor { get; init; }

    public string? Status { get; init; }

    public List<ImageInput> Images { get; init; } = new();

    public List<string> CollectionSlugs { get; init; } = new();
}

public sealed record UpdateVariantRequest
{
    [StringLength(160)]
    public string? Title { get; init; }

    [Required]
    [Range(0, 1_000_000)]
    public decimal? Price { get; init; }

    [StringLength(3, MinimumLength = 3)]
    public string? Currency { get; init; }

    [Range(0, 1_000_000)]
    public int? StockOnHand { get; init; }
}

public sealed record UpsertCollectionRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string? Title { get; init; }

    [Required, StringLength(160, MinimumLength = 1)]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Slug must be lowercase words separated by hyphens.")]
    public string? Slug { get; init; }

    [StringLength(2000)]
    public string? Description { get; init; }
}
