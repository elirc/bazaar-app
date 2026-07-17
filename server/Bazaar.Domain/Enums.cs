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
