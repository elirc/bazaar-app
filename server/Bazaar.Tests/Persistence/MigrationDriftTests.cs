using Bazaar.Infrastructure.Auth;
using Bazaar.Infrastructure.Persistence;
using Bazaar.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bazaar.Tests.Persistence;

/// <summary>
/// Guards against migration drift. If the EF model no longer matches the latest migration snapshot,
/// a migration is missing and a deployed database created by <c>Migrate()</c> would diverge from the
/// code's expectations. The companion test proves every seeder runs against a real, migrated schema.
/// </summary>
public class MigrationDriftTests
{
    [Fact]
    public void The_model_has_no_pending_changes_not_captured_in_a_migration()
    {
        using var testDb = new TestDb();
        using var ctx = testDb.NewContext();

        var differ = ctx.GetService<IMigrationsModelDiffer>();
        var snapshot = ctx.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot); // migrations exist and carry a model snapshot

        // Make the snapshot model comparable to the live design-time model. Older EF snapshots hand back
        // a still-mutable model that must be finalized + initialized; EF Core 9+ hands back one that is
        // already finalized and read-only, in which case it is used as-is.
        var initializer = ctx.GetService<IModelRuntimeInitializer>();
        var snapshotModel = snapshot!.Model;
        try
        {
            if (snapshotModel is IMutableModel mutable)
                snapshotModel = initializer.Initialize(mutable.FinalizeModel());
        }
        catch (InvalidOperationException)
        {
            snapshotModel = snapshot.Model; // already finalized/read-only
        }

        var currentModel = ctx.GetService<IDesignTimeModel>().Model;

        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.True(
            differences.Count == 0,
            "The EF model has changes not captured in a migration. Run `dotnet ef migrations add`. Pending operations: "
                + string.Join(", ", differences.Select(d => d.GetType().Name)));
    }

    [Fact]
    public async Task All_seeders_run_cleanly_on_a_freshly_migrated_database()
    {
        using var testDb = new TestDb();
        await using var ctx = testDb.NewContext(); // schema created by applying the real migrations

        await CatalogSeeder.SeedAsync(ctx);
        await ShippingSeeder.SeedAsync(ctx);
        await TaxAndGiftCardSeeder.SeedAsync(ctx);
        await AccountSeeder.SeedAsync(ctx, new Pbkdf2PasswordHasher());

        Assert.Equal(6, await ctx.Products.CountAsync());
        Assert.Equal(3, await ctx.ShippingMethods.CountAsync());
        Assert.Equal(3, await ctx.TaxZones.CountAsync());
        Assert.Equal(2, await ctx.Customers.CountAsync()); // seeded admin + demo customer

        // Every seeder is idempotent against the same migrated database.
        await CatalogSeeder.SeedAsync(ctx);
        await ShippingSeeder.SeedAsync(ctx);
        await TaxAndGiftCardSeeder.SeedAsync(ctx);
        await AccountSeeder.SeedAsync(ctx, new Pbkdf2PasswordHasher());

        Assert.Equal(6, await ctx.Products.CountAsync());
        Assert.Equal(2, await ctx.Customers.CountAsync());
    }
}
