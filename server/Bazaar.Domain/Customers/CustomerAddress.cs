using Bazaar.Domain.Common;

namespace Bazaar.Domain.Customers;

/// <summary>A saved address in a customer's address book. Wraps the reusable <see cref="Address"/> value object.</summary>
public class CustomerAddress
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }

    /// <summary>Optional friendly label, e.g. "Home" or "Work".</summary>
    public string? Label { get; set; }

    public bool IsDefault { get; set; }

    public Address Address { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
