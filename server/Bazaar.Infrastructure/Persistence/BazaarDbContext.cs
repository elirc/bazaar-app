using Bazaar.Domain;
using Bazaar.Domain.Carts;
using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;
using Bazaar.Domain.Customers;
using Bazaar.Domain.Discounts;
using Bazaar.Domain.Inventory;
using Bazaar.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bazaar.Infrastructure.Persistence;

public class BazaarDbContext : DbContext
{
    public BazaarDbContext(DbContextOptions<BazaarDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> Variants => Set<ProductVariant>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot order/compare DateTimeOffset: persist every one as UTC ticks (long).
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUtcTicksConverter>();

        // Store enums as their names for readable, stable columns.
        configurationBuilder.Properties<ProductStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<OrderStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<CartStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DiscountType>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Slug).IsRequired().HasMaxLength(160);
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.Title).IsRequired().HasMaxLength(200);
            e.Property(p => p.Description).HasMaxLength(4000);
            e.Property(p => p.Vendor).HasMaxLength(120);

            e.HasMany(p => p.Variants)
                .WithOne(v => v.Product!)
                .HasForeignKey(v => v.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);

            e.OwnsMany(p => p.Images, ib =>
            {
                ib.ToTable("ProductImages");
                ib.WithOwner().HasForeignKey("ProductId");
                ib.HasKey(i => i.Id);
                ib.Property(i => i.Url).IsRequired().HasMaxLength(1000);
                ib.Property(i => i.AltText).HasMaxLength(300);
            });
            e.Navigation(p => p.Images).UsePropertyAccessMode(PropertyAccessMode.Field);

            e.HasMany(p => p.Collections)
                .WithMany(c => c.Products)
                .UsingEntity("CollectionProducts");
            e.Navigation(p => p.Collections).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<ProductVariant>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Sku).IsRequired().HasMaxLength(80);
            e.HasIndex(v => v.Sku).IsUnique();
            e.Property(v => v.Title).IsRequired().HasMaxLength(160);

            e.OwnsOne(v => v.Price, mb => MapMoney(mb, "Price"));
            e.Navigation(v => v.Price).IsRequired();

            e.OwnsMany(v => v.Options, ob =>
            {
                ob.ToTable("VariantOptions");
                ob.WithOwner().HasForeignKey("VariantId");
                ob.Property(o => o.Name).IsRequired().HasMaxLength(60);
                ob.Property(o => o.Value).IsRequired().HasMaxLength(120);
                // Explicit composite key avoids a shadow int PK that SQLite won't auto-populate.
                ob.HasKey("VariantId", nameof(Bazaar.Domain.Catalog.VariantOption.Name));
            });
            e.Navigation(v => v.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<Collection>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Slug).IsRequired().HasMaxLength(160);
            e.HasIndex(c => c.Slug).IsUnique();
            e.Property(c => c.Title).IsRequired().HasMaxLength(200);
            e.Property(c => c.Description).HasMaxLength(2000);
            e.Navigation(c => c.Products).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<InventoryItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => i.VariantId).IsUnique();
            e.Ignore(i => i.Available);
            e.HasOne(i => i.Variant)
                .WithOne()
                .HasForeignKey<InventoryItem>(i => i.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Cart>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Token).IsRequired().HasMaxLength(64);
            e.HasIndex(c => c.Token).IsUnique();
            e.Ignore(c => c.TotalQuantity);

            e.HasMany(c => c.Items)
                .WithOne()
                .HasForeignKey(li => li.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<CartLineItem>(e =>
        {
            e.HasKey(li => li.Id);
            e.Property(li => li.Quantity).IsRequired();
            e.HasOne(li => li.Variant)
                .WithMany()
                .HasForeignKey(li => li.VariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Email).IsRequired().HasMaxLength(320);
            e.HasIndex(c => c.Email).IsUnique();
            e.Property(c => c.FirstName).HasMaxLength(120);
            e.Property(c => c.LastName).HasMaxLength(120);
        });

        b.Entity<DiscountCode>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Code).IsRequired().HasMaxLength(60);
            e.HasIndex(d => d.Code).IsUnique();
            e.Property(d => d.Currency).HasMaxLength(3);
            e.Property(d => d.Value).HasPrecision(18, 2);
        });

        b.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Number).IsRequired().HasMaxLength(30);
            e.HasIndex(o => o.Number).IsUnique();
            e.Property(o => o.Email).IsRequired().HasMaxLength(320);
            e.Property(o => o.Currency).IsRequired().HasMaxLength(3);
            e.Property(o => o.DiscountCode).HasMaxLength(60);

            e.OwnsOne(o => o.ShippingAddress, ab =>
            {
                ab.Property(a => a.Name).IsRequired().HasMaxLength(200);
                ab.Property(a => a.Line1).IsRequired().HasMaxLength(200);
                ab.Property(a => a.Line2).HasMaxLength(200);
                ab.Property(a => a.City).IsRequired().HasMaxLength(120);
                ab.Property(a => a.Region).HasMaxLength(120);
                ab.Property(a => a.PostalCode).IsRequired().HasMaxLength(20);
                ab.Property(a => a.Country).IsRequired().HasMaxLength(2);
            });
            e.Navigation(o => o.ShippingAddress).IsRequired();

            e.OwnsOne(o => o.Subtotal, mb => MapMoney(mb, "Subtotal"));
            e.OwnsOne(o => o.DiscountTotal, mb => MapMoney(mb, "DiscountTotal"));
            e.OwnsOne(o => o.TaxTotal, mb => MapMoney(mb, "TaxTotal"));
            e.OwnsOne(o => o.ShippingTotal, mb => MapMoney(mb, "ShippingTotal"));
            e.OwnsOne(o => o.GrandTotal, mb => MapMoney(mb, "GrandTotal"));
            e.Navigation(o => o.Subtotal).IsRequired();
            e.Navigation(o => o.DiscountTotal).IsRequired();
            e.Navigation(o => o.TaxTotal).IsRequired();
            e.Navigation(o => o.ShippingTotal).IsRequired();
            e.Navigation(o => o.GrandTotal).IsRequired();

            e.HasMany(o => o.Items)
                .WithOne()
                .HasForeignKey(li => li.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<OrderLineItem>(e =>
        {
            e.HasKey(li => li.Id);
            e.Property(li => li.Sku).IsRequired().HasMaxLength(80);
            e.Property(li => li.Title).IsRequired().HasMaxLength(200);
            e.OwnsOne(li => li.UnitPrice, mb => MapMoney(mb, "UnitPrice"));
            e.OwnsOne(li => li.LineTotal, mb => MapMoney(mb, "LineTotal"));
            e.Navigation(li => li.UnitPrice).IsRequired();
            e.Navigation(li => li.LineTotal).IsRequired();
        });

        // Guid primary keys are assigned by the domain (in entity initializers), not by the store.
        // Telling EF they are never store-generated ensures a new child added to an already-tracked
        // aggregate (e.g. a cart line) is INSERTed, not mistaken for an existing row and UPDATEd.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.IsPrimaryKey() && p.ClrType == typeof(Guid)))
        {
            property.ValueGenerated = ValueGenerated.Never;
        }
    }

    private static void MapMoney<T>(OwnedNavigationBuilder<T, Money> mb, string prefix) where T : class
    {
        mb.Property(m => m.Amount)
            .HasColumnName(prefix + "Amount")
            .HasConversion(new DecimalToCentsConverter());
        mb.Property(m => m.Currency)
            .HasColumnName(prefix + "Currency")
            .IsRequired()
            .HasMaxLength(3);
        mb.Ignore(m => m.IsNegative);
        mb.Ignore(m => m.IsZero);
    }
}
