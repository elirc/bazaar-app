namespace Bazaar.Domain.Customers;

/// <summary>A known customer, identified by email.</summary>
public class Customer
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
