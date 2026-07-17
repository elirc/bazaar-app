using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class CheckoutFlowTests : IClassFixture<BazaarApiFactory>
{
    private readonly HttpClient _client;

    public CheckoutFlowTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> VariantId(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<string> CartWith(string slug, string sku, int quantity)
    {
        var create = await _client.PostAsync("/api/cart", null);
        var cart = await create.Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(slug, sku);
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = variantId, Quantity = quantity });
        return cart.Token;
    }

    private static CheckoutRequest Checkout(string token, string email = "buyer@example.com") => new()
    {
        CartToken = token,
        Email = email,
        ShippingAddress = new AddressInput
        {
            Name = "Ada Lovelace",
            Line1 = "1 Analytical Way",
            City = "London",
            PostalCode = "EC1A",
            Country = "GB",
        },
    };

    [Fact]
    public async Task Checkout_computes_totals_and_places_a_paid_order()
    {
        var token = await CartWith("ceramic-mug", "MUG-CRM", 2); // 2 x $14.00

        var response = await _client.PostAsJsonAsync("/api/checkout", Checkout(token));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("Paid", order!.Status);
        Assert.Equal(28.00m, order.Subtotal.Amount);
        Assert.Equal(2.31m, order.TaxTotal.Amount);       // 8.25%
        Assert.Equal(5.99m, order.ShippingTotal.Amount);  // under free-shipping threshold
        Assert.Equal(36.30m, order.GrandTotal.Amount);
        Assert.StartsWith("BZ-", order.Number);

        // Order is retrievable
        var fetched = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        Assert.Equal(order.Number, fetched!.Number);
    }

    [Fact]
    public async Task Checkout_reduces_available_stock()
    {
        var before = await _client.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/leather-belt");
        var available = before!.Variants.Single(v => v.Sku == "BELT-32").Available;

        var token = await CartWith("leather-belt", "BELT-32", 3);
        var response = await _client.PostAsJsonAsync("/api/checkout", Checkout(token));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = await _client.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/leather-belt");
        Assert.Equal(available - 3, after!.Variants.Single(v => v.Sku == "BELT-32").Available);
    }

    [Fact]
    public async Task Checkout_over_the_free_shipping_threshold_is_free()
    {
        var token = await CartWith("merino-hoodie", "HOOD-M-GRY", 1); // $89.00

        var order = await (await _client.PostAsJsonAsync("/api/checkout", Checkout(token)))
            .Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(89.00m, order!.Subtotal.Amount);
        Assert.Equal(0m, order.ShippingTotal.Amount);
    }

    [Fact]
    public async Task Checkout_with_an_empty_cart_returns_409()
    {
        var create = await _client.PostAsync("/api/cart", null);
        var cart = await create.Content.ReadFromJsonAsync<CartDto>();

        var response = await _client.PostAsJsonAsync("/api/checkout", Checkout(cart!.Token));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_with_insufficient_stock_returns_409()
    {
        var token = await CartWith("wool-blanket", "BLNK-OAT", 9); // only 8 on hand

        var response = await _client.PostAsJsonAsync("/api/checkout", Checkout(token));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_with_a_declined_card_returns_402_and_keeps_stock()
    {
        var before = await _client.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/canvas-tote");
        var available = before!.Variants.Single(v => v.Sku == "TOTE-NAT").Available;

        var token = await CartWith("canvas-tote", "TOTE-NAT", 1);
        var response = await _client.PostAsJsonAsync("/api/checkout", Checkout(token, "decline@example.com"));
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var after = await _client.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/canvas-tote");
        Assert.Equal(available, after!.Variants.Single(v => v.Sku == "TOTE-NAT").Available);
    }

    [Fact]
    public async Task Checkout_with_missing_address_fields_returns_400()
    {
        var token = await CartWith("ceramic-mug", "MUG-CRM", 1);
        var request = new CheckoutRequest
        {
            CartToken = token,
            Email = "buyer@example.com",
            ShippingAddress = new AddressInput { Name = "No Address" },
        };

        var response = await _client.PostAsJsonAsync("/api/checkout", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
