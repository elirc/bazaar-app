namespace Bazaar.Domain;

public enum ProductStatus
{
    Draft,
    Active,
    Archived,
}

public enum CartStatus
{
    Open,
    Converted,
    Abandoned,
}

/// <summary>Order lifecycle: Pending -> Paid -> Fulfilled, with Cancelled/Refunded terminal states.</summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Fulfilled,
    Cancelled,
    Refunded,
}

public enum DiscountType
{
    Percentage,
    FixedAmount,
}
