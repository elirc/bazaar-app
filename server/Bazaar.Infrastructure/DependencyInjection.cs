using Bazaar.Domain.Payments;
using Bazaar.Infrastructure.Auth;
using Bazaar.Infrastructure.Checkout;
using Bazaar.Infrastructure.Fulfillment;
using Bazaar.Infrastructure.Payments;
using Bazaar.Infrastructure.Persistence;
using Bazaar.Infrastructure.Returns;
using Bazaar.Infrastructure.Tax;
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
        services.AddScoped<ITaxService, ZoneTaxService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<ReturnService>();
        services.AddScoped<FulfillmentService>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}
