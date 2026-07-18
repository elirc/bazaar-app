namespace Bazaar.Domain.Returns;

/// <summary>A single order line being returned, with the quantity requested for return.</summary>
public class ReturnLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ReturnRequestId { get; set; }
    public Guid OrderLineItemId { get; set; }
    public Guid? VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
