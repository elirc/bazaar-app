namespace Bazaar.Domain.Customers;

/// <summary>A known customer, identified by email. May authenticate with a hashed password.</summary>
public class Customer
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Salted password hash (see the infrastructure password hasher). Empty for guest-only records.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public CustomerRole Role { get; set; } = CustomerRole.Customer;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string DisplayName =>
        string.Join(' ', new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n
            ? n
            : Email;
}
