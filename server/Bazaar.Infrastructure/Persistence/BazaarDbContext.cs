using Bazaar.Domain;
using Bazaar.Domain.Carts;
using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;
using Bazaar.Domain.Customers;
using Bazaar.Domain.Discounts;
using Bazaar.Domain.GiftCards;
using Bazaar.Domain.Inventory;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Returns;
using Bazaar.Domain.Reviews;
using Bazaar.Domain.Shipping;
using Bazaar.Domain.Tax;
using Bazaar.Domain.Webhooks;
using Bazaar.Domain.Wishlists;
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
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ReviewVote> ReviewVotes => Set<ReviewVote>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnLine> ReturnLines => Set<ReturnLine>();
    public DbSet<TaxZone> TaxZones => Set<TaxZone>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentLine> ShipmentLines => Set<ShipmentLine>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RefreshConcurrencyStamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RefreshConcurrencyStamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Assign a fresh concurrency stamp to every modified stamped entity so stale writes conflict.</summary>
    private void RefreshConcurrencyStamps()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyStamped>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();
        }
    }

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
        configurationBuilder.Properties<CustomerRole>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ShippingRateType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ReviewStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ReturnStatus>().HaveConversion<string>().HaveMaxLength(20);
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
            e.Property(p => p.TaxCategory).IsRequired().HasMaxLength(40);

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
            e.Property(i => i.ConcurrencyStamp).IsConcurrencyToken();
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
            e.HasIndex(c => c.CustomerId);
            e.Property(c => c.ConcurrencyStamp).IsConcurrencyToken();
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
            e.Property(c => c.PasswordHash).IsRequired().HasMaxLength(400);
            e.Ignore(c => c.DisplayName);
        });

        b.Entity<CustomerAddress>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.CustomerId);
            e.Property(a => a.Label).HasMaxLength(60);
            e.OwnsOne(a => a.Address, ab =>
            {
                ab.Property(x => x.Name).IsRequired().HasMaxLength(200);
                ab.Property(x => x.Line1).IsRequired().HasMaxLength(200);
                ab.Property(x => x.Line2).HasMaxLength(200);
                ab.Property(x => x.City).IsRequired().HasMaxLength(120);
                ab.Property(x => x.Region).HasMaxLength(120);
                ab.Property(x => x.PostalCode).IsRequired().HasMaxLength(20);
                ab.Property(x => x.Country).IsRequired().HasMaxLength(2);
            });
            e.Navigation(a => a.Address).IsRequired();
        });

        b.Entity<ShippingMethod>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Code).IsRequired().HasMaxLength(40);
            e.HasIndex(s => s.Code).IsUnique();
            e.Property(s => s.Name).IsRequired().HasMaxLength(120);
            e.Property(s => s.PerKgRate).HasPrecision(18, 2);
            e.Property(s => s.FreeThreshold).HasPrecision(18, 2);
            e.OwnsOne(s => s.BaseRate, mb => MapMoney(mb, "BaseRate"));
            e.Navigation(s => s.BaseRate).IsRequired();
            e.Ignore(s => s.DeliveryEstimate);
        });

        b.Entity<ProductReview>(e =>
        {
            e.HasKey(r => r.Id);
            // One review per customer per product.
            e.HasIndex(r => new { r.ProductId, r.CustomerId }).IsUnique();
            e.HasIndex(r => r.Status);
            e.Property(r => r.AuthorName).IsRequired().HasMaxLength(200);
            e.Property(r => r.Title).HasMaxLength(160);
            e.Property(r => r.Body).IsRequired().HasMaxLength(4000);
        });

        b.Entity<ReviewVote>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.ReviewId, v.CustomerId }).IsUnique();
        });

        b.Entity<Wishlist>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.CustomerId);
            e.Property(w => w.Name).IsRequired().HasMaxLength(120);
            e.HasMany(w => w.Items)
                .WithOne()
                .HasForeignKey(i => i.WishlistId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(w => w.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<WishlistItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.HasIndex(i => new { i.WishlistId, i.VariantId }).IsUnique();
            e.HasOne(i => i.Variant)
                .WithMany()
                .HasForeignKey(i => i.VariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ReturnRequest>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.OrderId);
            e.HasIndex(r => r.CustomerId);
            e.HasIndex(r => r.Status);
            e.Property(r => r.Reason).HasMaxLength(1000);
            e.Property(r => r.RefundReference).HasMaxLength(80);

            e.OwnsOne(r => r.RefundAmount, mb => MapMoney(mb, "Refund"));
            e.Navigation(r => r.RefundAmount).IsRequired();

            e.HasMany(r => r.Lines)
                .WithOne()
                .HasForeignKey(l => l.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<ReturnLine>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Sku).IsRequired().HasMaxLength(80);
            e.Property(l => l.Title).IsRequired().HasMaxLength(200);
        });

        b.Entity<TaxZone>(e =>
        {
            e.HasKey(z => z.Id);
            e.Property(z => z.Name).IsRequired().HasMaxLength(120);
            e.Property(z => z.Country).IsRequired().HasMaxLength(2);
            e.Property(z => z.Region).HasMaxLength(120);
            e.Property(z => z.StandardRate).HasPrecision(9, 5);
            e.HasIndex(z => new { z.Country, z.Region });

            e.OwnsMany(z => z.CategoryRates, rb =>
            {
                rb.ToTable("TaxCategoryRates");
                rb.WithOwner().HasForeignKey("TaxZoneId");
                rb.Property(r => r.Category).IsRequired().HasMaxLength(40);
                rb.Property(r => r.Rate).HasPrecision(9, 5);
                rb.HasKey("TaxZoneId", nameof(TaxCategoryRate.Category));
            });
            e.Navigation(z => z.CategoryRates).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<GiftCard>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Code).IsRequired().HasMaxLength(40);
            e.HasIndex(g => g.Code).IsUnique();
            e.OwnsOne(g => g.InitialBalance, mb => MapMoney(mb, "Initial"));
            e.OwnsOne(g => g.Balance, mb => MapMoney(mb, "Balance"));
            e.Navigation(g => g.InitialBalance).IsRequired();
            e.Navigation(g => g.Balance).IsRequired();
            e.Ignore(g => g.IsRedeemable);
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
            e.HasIndex(o => o.CustomerId);
            e.Property(o => o.Email).IsRequired().HasMaxLength(320);
            e.Property(o => o.Currency).IsRequired().HasMaxLength(3);
            e.Property(o => o.DiscountCode).HasMaxLength(60);
            e.Property(o => o.ShippingMethod).HasMaxLength(120);
            e.Property(o => o.GiftCardCode).HasMaxLength(40);

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
            e.OwnsOne(o => o.GiftCardTotal, mb => MapMoney(mb, "GiftCardTotal"));
            e.Navigation(o => o.Subtotal).IsRequired();
            e.Navigation(o => o.DiscountTotal).IsRequired();
            e.Navigation(o => o.TaxTotal).IsRequired();
            e.Navigation(o => o.ShippingTotal).IsRequired();
            e.Navigation(o => o.GrandTotal).IsRequired();
            e.Navigation(o => o.GiftCardTotal).IsRequired();

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

        b.Entity<Shipment>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.OrderId);
            e.Property(s => s.Carrier).IsRequired().HasMaxLength(80);
            e.Property(s => s.TrackingNumber).IsRequired().HasMaxLength(120);
            e.HasMany(s => s.Lines)
                .WithOne()
                .HasForeignKey(l => l.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Navigation(s => s.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<ShipmentLine>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Sku).IsRequired().HasMaxLength(80);
            e.Property(l => l.Title).IsRequired().HasMaxLength(200);
        });

        b.Entity<WebhookSubscription>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Url).IsRequired().HasMaxLength(1000);
            e.Property(w => w.Secret).IsRequired().HasMaxLength(200);
            e.Property(w => w.Events).IsRequired().HasMaxLength(400);
            e.Ignore(w => w.EventList);
        });

        b.Entity<WebhookDelivery>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.SubscriptionId);
            e.Property(d => d.Event).IsRequired().HasMaxLength(60);
            e.Property(d => d.Url).IsRequired().HasMaxLength(1000);
            e.Property(d => d.Payload).IsRequired().HasMaxLength(8000);
            e.Property(d => d.Signature).IsRequired().HasMaxLength(128);
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
