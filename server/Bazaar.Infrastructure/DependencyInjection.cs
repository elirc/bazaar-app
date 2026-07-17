using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bazaar.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the EF Core SQLite persistence for Bazaar.</summary>
    public static IServiceCollection AddBazaarInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BazaarDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }
}
