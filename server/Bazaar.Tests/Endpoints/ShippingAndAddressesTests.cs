using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class ShippingAndAddressesTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public ShippingAndAddressesTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> VariantId(HttpClient client, string slug, string sku)
    {
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<string> CartWith(HttpClient client, string slug, string sku, int quantity)
    {
        var cart = await (await client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(client, slug, sku);
        await client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = variantId, Quantity = quantity });
        return cart.Token;
    }

    private static CheckoutRequest Checkout(string token, string? shippingCode = null) => new()
    {
        CartToken = token,
        Email = "buyer@example.com",
        ShippingMethodCode = shippingCode,
        ShippingAddress = new AddressInput
        {
            Name = "Ada Lovelace", Line1 = "1 Analytical Way", City = "London",
            PostalCode = "EC1A", Country = "GB",
        },
    };

    [Fact]
    public async Task Shipping_options_price_each_method_for_the_cart()
    {
        var client = _factory.CreateClient();
        var token = await CartWith(client, "wool-blanket", "BLNK-OAT", 1); // $120, 1500g

        var options = await client.GetFromJsonAsync<List<ShippingOptionDto>>(
            $"/api/checkout/shipping-options?cartToken={token}");

        Assert.NotNull(options);
        var standard = options!.Single(o => o.Code == "standard");
        var express = options.Single(o => o.Code == "express");
        var freight = options.Single(o => o.Code == "freight");

        Assert.Equal(0m, standard.Cost.Amount);       // subtotal 120 >= 75 free threshold
        Assert.Equal(14.99m, express.Cost.Amount);     // flat
        Assert.Equal(6.24m, freight.Cost.Amount);      // 3.99 + 1.50 * 1.5kg
        Assert.False(string.IsNullOrWhiteSpace(freight.DeliveryEstimate));
    }

    [Fact]
    public async Task Checkout_uses_the_default_method_when_none_is_selected()
    {
        var client = _factory.CreateClient();
        var token = await CartWith(client, "ceramic-mug", "MUG-CRM", 2); // $28, under threshold

        var order = await (await client.PostAsJsonAsync("/api/checkout", Checkout(token)))
            .Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(5.99m, order!.ShippingTotal.Amount);
        Assert.Equal("Standard", order.ShippingMethod);
    }

    [Fact]
    public async Task Checkout_with_express_charges_the_flat_rate()
    {
        var client = _factory.CreateClient();
        var token = await CartWith(client, "ceramic-mug", "MUG-CRM", 2);

        var order = await (await client.PostAsJsonAsync("/api/checkout", Checkout(token, "express")))
            .Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(14.99m, order!.ShippingTotal.Amount);
        Assert.Equal("Express", order.ShippingMethod);
    }

    [Fact]
    public async Task Checkout_with_an_unknown_shipping_method_returns_400()
    {
        var client = _factory.CreateClient();
        var token = await CartWith(client, "ceramic-mug", "MUG-CRM", 1);

        var response = await client.PostAsJsonAsync("/api/checkout", Checkout(token, "teleport"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Address_book_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/addresses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static UpsertAddressRequest Address(string label, bool isDefault, string city = "Denver") => new()
    {
        Label = label,
        IsDefault = isDefault,
        Address = new AddressInput
        {
            Name = "Grace Hopper", Line1 = "1 Navy Yard", City = city,
            PostalCode = "22202", Country = "US",
        },
    };

    [Fact]
    public async Task First_address_becomes_the_default_and_a_new_default_replaces_it()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"addr-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);

        var first = await (await client.PostAsJsonAsync("/api/account/addresses", Address("Home", isDefault: false)))
            .Content.ReadFromJsonAsync<CustomerAddressDto>();
        Assert.True(first!.IsDefault); // first address is default even though not requested

        var second = await (await client.PostAsJsonAsync("/api/account/addresses", Address("Work", isDefault: true)))
            .Content.ReadFromJsonAsync<CustomerAddressDto>();
        Assert.True(second!.IsDefault);

        var list = await client.GetFromJsonAsync<List<CustomerAddressDto>>("/api/account/addresses");
        Assert.Equal(2, list!.Count);
        Assert.Single(list, a => a.IsDefault);
        Assert.Equal(second.Id, list.Single(a => a.IsDefault).Id);
    }

    [Fact]
    public async Task A_customer_cannot_touch_another_customers_address()
    {
        var alice = _factory.CreateClient();
        var aliceAuth = await alice.RegisterAsync($"alice-addr-{Guid.NewGuid():N}@example.com", "supersecret");
        alice.UseBearer(aliceAuth.Token);
        var aliceAddr = await (await alice.PostAsJsonAsync("/api/account/addresses", Address("Home", false)))
            .Content.ReadFromJsonAsync<CustomerAddressDto>();

        var bob = _factory.CreateClient();
        var bobAuth = await bob.RegisterAsync($"bob-addr-{Guid.NewGuid():N}@example.com", "supersecret");
        bob.UseBearer(bobAuth.Token);

        var update = await bob.PutAsJsonAsync($"/api/account/addresses/{aliceAddr!.Id}", Address("Hijack", true));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var delete = await bob.DeleteAsync($"/api/account/addresses/{aliceAddr.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
