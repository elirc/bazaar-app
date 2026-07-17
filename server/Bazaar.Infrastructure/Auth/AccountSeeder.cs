using Bazaar.Domain;
using Bazaar.Domain.Customers;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Auth;

/// <summary>Seeds a development admin account (and a demo customer) so the app is usable out of the box.</summary>
public static class AccountSeeder
{
    public const string AdminEmail = "admin@bazaar.test";
    public const string AdminPassword = "admin-dev-password";
    public const string CustomerEmail = "shopper@bazaar.test";
    public const string CustomerPassword = "shopper-dev-password";

    public static async Task SeedAsync(BazaarDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Customers.AnyAsync(ct))
            return;

        db.Customers.Add(new Customer
        {
            Email = AdminEmail,
            FirstName = "Bazaar",
            LastName = "Admin",
            Role = CustomerRole.Admin,
            PasswordHash = hasher.Hash(AdminPassword),
        });

        db.Customers.Add(new Customer
        {
            Email = CustomerEmail,
            FirstName = "Sam",
            LastName = "Shopper",
            Role = CustomerRole.Customer,
            PasswordHash = hasher.Hash(CustomerPassword),
        });

        await db.SaveChangesAsync(ct);
    }
}
