using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record ShipmentLineDto(
    Guid OrderLineItemId,
    string Sku,
    string Title,
    int Quantity);

public sealed record ShipmentDto(
    Guid Id,
    string Carrier,
    string TrackingNumber,
    DateTimeOffset ShippedAt,
    IReadOnlyList<ShipmentLineDto> Lines);

public sealed record CreateShipmentLineInput
{
    [Required]
    public Guid? OrderLineItemId { get; init; }

    [Range(1, 100000)]
    public int Quantity { get; init; }
}

public sealed record CreateShipmentRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string? Carrier { get; init; }

    [Required, StringLength(120, MinimumLength = 1)]
    public string? TrackingNumber { get; init; }

    [MinLength(1, ErrorMessage = "A shipment must include at least one line.")]
    public List<CreateShipmentLineInput> Lines { get; init; } = new();
}
