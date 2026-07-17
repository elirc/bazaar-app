using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bazaar.Infrastructure.Persistence;

/// <summary>
/// Enables `dotnet ef` tooling to construct the context at design time without booting the API.
/// </summary>
public sealed class BazaarDbContextFactory : IDesignTimeDbContextFactory<BazaarDbContext>
{
    public BazaarDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BazaarDbContext>()
            .UseSqlite("Data Source=bazaar-design.db")
            .Options;
        return new BazaarDbContext(options);
    }
}
