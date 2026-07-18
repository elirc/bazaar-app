using System.Text;
using Bazaar.Api;
using Bazaar.Api.Endpoints;
using Bazaar.Infrastructure;
using Bazaar.Infrastructure.Auth;
using Bazaar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("Bazaar")
    ?? "Data Source=bazaar.db";
builder.Services.AddBazaarInfrastructure(connectionString);

// Authentication & authorization. The signing key is shared between token issuance (infrastructure)
// and the JwtBearer validation configured here.
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)),
            ValidateLifetime = true,
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Apply migrations and seed the development catalog + accounts on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BazaarDbContext>();
    await db.Database.MigrateAsync();
    await CatalogSeeder.SeedAsync(db);
    await ShippingSeeder.SeedAsync(db);
    await TaxAndGiftCardSeeder.SeedAsync(db);
    await AccountSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
}

app.MapGet("/", () => "Bazaar API");
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "bazaar-api" }));

app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapWishlistEndpoints();
app.MapStorefrontEndpoints();
app.MapReviewEndpoints();
app.MapAdminCatalogEndpoints();
app.MapAdminOrderEndpoints();
app.MapAdminDiscountEndpoints();
app.MapAdminReviewEndpoints();
app.MapAdminReturnEndpoints();
app.MapAdminReportEndpoints();
app.MapAdminWebhookEndpoints();
app.MapCartEndpoints();
app.MapCheckoutEndpoints();
app.MapShippingEndpoints();
app.MapGiftCardEndpoints();

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program { }
