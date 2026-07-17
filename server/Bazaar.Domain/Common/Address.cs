namespace Bazaar.Domain.Common;

/// <summary>A postal address captured at checkout. Immutable value object.</summary>
public sealed record Address
{
    public required string Name { get; init; }
    public required string Line1 { get; init; }
    public string? Line2 { get; init; }
    public required string City { get; init; }
    public string? Region { get; init; }
    public required string PostalCode { get; init; }
    public required string Country { get; init; }
}
