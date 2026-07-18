using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class WishlistsTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public WishlistsTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> NewCustomer()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"wish-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);
        return client;
    }

    private async Task<Guid> VariantId(HttpClient client, string slug, string sku)
    {
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    [Fact]
    public async Task Wishlists_require_authentication()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/wishlists")).StatusCode);
    }

    [Fact]
    public async Task Listing_creates_a_default_wishlist()
    {
        var client = await NewCustomer();
        var wishlists = await client.GetFromJsonAsync<List<WishlistDto>>("/api/account/wishlists");
        Assert.Single(wishlists!);
        Assert.True(wishlists![0].IsDefault);
    }

    [Fact]
    public async Task Items_can_be_added_to_the_default_wishlist_and_a_named_one()
    {
        var client = await NewCustomer();
        var mug = await VariantId(client, "ceramic-mug", "MUG-CRM");

        var toDefault = await client.PostAsJsonAsync("/api/account/wishlist/items", new AddWishlistItemRequest { VariantId = mug });
        var defaultList = await toDefault.Content.ReadFromJsonAsync<WishlistDto>();
        Assert.Contains(defaultList!.Items, i => i.VariantId == mug);

        var named = await (await client.PostAsJsonAsync("/api/account/wishlists", new CreateWishlistRequest { Name = "Gifts" }))
            .Content.ReadFromJsonAsync<WishlistDto>();
        var tee = await VariantId(client, "classic-tee", "TEE-M-BLK");
        var updated = await (await client.PostAsJsonAsync($"/api/account/wishlists/{named!.Id}/items",
            new AddWishlistItemRequest { VariantId = tee })).Content.ReadFromJsonAsync<WishlistDto>();
        Assert.Contains(updated!.Items, i => i.VariantId == tee);
    }

    [Fact]
    public async Task The_default_wishlist_cannot_be_deleted()
    {
        var client = await NewCustomer();
        var wishlists = await client.GetFromJsonAsync<List<WishlistDto>>("/api/account/wishlists");
        var response = await client.DeleteAsync($"/api/account/wishlists/{wishlists![0].Id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Moving_a_wishlist_item_to_cart_adds_it_and_removes_it_from_the_wishlist()
    {
        var client = await NewCustomer();
        var mug = await VariantId(client, "ceramic-mug", "MUG-CRM");
        var list = await (await client.PostAsJsonAsync("/api/account/wishlist/items", new AddWishlistItemRequest { VariantId = mug }))
            .Content.ReadFromJsonAsync<WishlistDto>();

        var moved = await client.PostAsJsonAsync(
            $"/api/account/wishlists/{list!.Id}/items/{mug}/move-to-cart", new MoveToCartRequest());
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var cart = await moved.Content.ReadFromJsonAsync<CartDto>();
        Assert.Contains(cart!.Items, i => i.VariantId == mug && !i.SavedForLater);

        var after = await client.GetFromJsonAsync<List<WishlistDto>>("/api/account/wishlists");
        Assert.DoesNotContain(after!.SelectMany(w => w.Items), i => i.VariantId == mug);
    }

    [Fact]
    public async Task Saved_for_later_lines_are_excluded_from_totals_and_checkout()
    {
        var client = await NewCustomer();
        var mug = await VariantId(client, "ceramic-mug", "MUG-CRM");
        var cart = await (await client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        await client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = mug, Quantity = 2 });

        var saved = await (await client.PostAsJsonAsync($"/api/cart/{cart.Token}/items/{mug}/saved",
            new SaveForLaterRequest { Saved = true })).Content.ReadFromJsonAsync<CartDto>();
        Assert.Equal(0, saved!.ItemCount);
        Assert.Equal(1, saved.SavedCount);
        Assert.Equal(0m, saved.Subtotal.Amount);

        // A cart containing only saved-for-later lines checks out as empty.
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        var response = await client.PostAsJsonAsync("/api/checkout", checkout);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Back_in_stock_flag_flips_when_a_saved_variant_is_restocked()
    {
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();

        // Admin creates an out-of-stock product.
        var slug = $"oos-{Guid.NewGuid():N}".Substring(0, 20);
        var create = await admin.PostAsJsonAsync("/api/admin/products", new CreateProductRequest
        {
            Title = "Out of stock demo",
            Slug = slug,
            Status = "Active",
            Images = new() { new ImageInput { Url = "https://images.bazaar.test/x.jpg", Position = 0 } },
            Variants = new() { new VariantInput { Sku = $"OOS-{Guid.NewGuid():N}".Substring(0, 12), Price = 10m, StockOnHand = 0 } },
        });
        var product = await create.Content.ReadFromJsonAsync<ProductDetailDto>();
        var variantId = product!.Variants[0].Id;

        var customer = await NewCustomer();
        var added = await (await customer.PostAsJsonAsync("/api/account/wishlist/items",
            new AddWishlistItemRequest { VariantId = variantId })).Content.ReadFromJsonAsync<WishlistDto>();
        Assert.False(added!.Items.Single(i => i.VariantId == variantId).BackInStock); // still out of stock

        // Admin restocks the variant.
        await admin.PutAsJsonAsync($"/api/admin/variants/{variantId}", new UpdateVariantRequest { Price = 10m, StockOnHand = 5 });

        var refreshed = await customer.GetFromJsonAsync<List<WishlistDto>>("/api/account/wishlists");
        Assert.True(refreshed!.SelectMany(w => w.Items).Single(i => i.VariantId == variantId).BackInStock);
    }
}
