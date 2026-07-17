using Bazaar.Domain.Checkout;
using Bazaar.Domain.Payments;
using Bazaar.Infrastructure.Auth;
using Bazaar.Infrastructure.Checkout;
using Bazaar.Infrastructure.Payments;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bazaar.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the EF Core SQLite persistence and checkout services for Bazaar.</summary>
    public static IServiceCollection AddBazaarInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BazaarDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        services.AddSingleton<ITaxCalculator, FlatRateTaxCalculator>();
        services.AddSingleton<IShippingCalculator, ThresholdShippingCalculator>();
        services.AddScoped<CheckoutService>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
