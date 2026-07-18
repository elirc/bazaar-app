using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record ReturnLineDto(
    Guid OrderLineItemId,
    string Sku,
    string Title,
    int Quantity);

public sealed record ReturnRequestDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Status,
    string? Reason,
    MoneyDto RefundAmount,
    IReadOnlyList<ReturnLineDto> Lines,
    DateTimeOffset CreatedAt);

public sealed record AdminReturnDto(
    Guid Id,
    Guid OrderId,
    string OrderNumber,
    string Email,
    string Status,
    string? Reason,
    MoneyDto RefundAmount,
    IReadOnlyList<ReturnLineDto> Lines,
    DateTimeOffset CreatedAt);

public sealed record CreateReturnLineInput
{
    [Required]
    public Guid? OrderLineItemId { get; init; }

    [Range(1, 1000)]
    public int Quantity { get; init; }
}

public sealed record CreateReturnRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }

    [MinLength(1, ErrorMessage = "A return must include at least one line.")]
    public List<CreateReturnLineInput> Lines { get; init; } = new();
}

public sealed record RejectReturnRequest
{
    [StringLength(1000)]
    public string? Reason { get; init; }
}
