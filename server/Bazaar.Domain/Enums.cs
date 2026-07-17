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

/// <summary>Access level for a customer account. Admins may reach the back-office endpoints.</summary>
public enum CustomerRole
{
    Customer,
    Admin,
}

/// <summary>How a shipping method prices an order.</summary>
public enum ShippingRateType
{
    /// <summary>A single flat fee regardless of cart contents.</summary>
    Flat,

    /// <summary>A base fee plus a per-kilogram surcharge on the cart weight.</summary>
    Weight,

    /// <summary>A flat fee that becomes free once the subtotal reaches a threshold.</summary>
    FreeOverThreshold,
}
