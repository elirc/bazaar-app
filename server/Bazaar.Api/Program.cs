using Bazaar.Infrastructure;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("Bazaar")
    ?? "Data Source=bazaar.db";
builder.Services.AddBazaarInfrastructure(connectionString);

var app = builder.Build();

app.UseCors();

// Apply migrations and seed the development catalog on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BazaarDbContext>();
    await db.Database.MigrateAsync();
    await CatalogSeeder.SeedAsync(db);
}

app.MapGet("/", () => "Bazaar API");
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "bazaar-api" }));

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program { }
