using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class OrdersAndDiscountsTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public OrdersAndDiscountsTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _client.AuthenticateAdminAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> VariantId(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<int> Available(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Available;
    }

    private async Task<string> CartWith(string slug, string sku, int quantity)
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(slug, sku);
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = variantId, Quantity = quantity });
        return cart.Token;
    }

    private static CheckoutRequest Checkout(string token, string? discountCode = null) => new()
    {
        CartToken = token,
        Email = "buyer@example.com",
        DiscountCode = discountCode,
        ShippingAddress = new AddressInput
        {
            Name = "Grace Hopper", Line1 = "1 Navy Yard", City = "Arlington",
            PostalCode = "22202", Country = "US",
        },
    };

    [Fact]
    public async Task Checkout_applies_a_percentage_discount()
    {
        var token = await CartWith("ceramic-mug", "MUG-CRM", 2); // $28.00

        var order = await (await _client.PostAsJsonAsync("/api/checkout", Checkout(token, "WELCOME10")))
            .Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal("WELCOME10", order!.DiscountCode);
        Assert.Equal(2.80m, order.DiscountTotal.Amount);            // 10% of 28.00
        Assert.Equal(33.50m, order.GrandTotal.Amount);             // 28 + 2.31 tax + 5.99 ship - 2.80
    }

    [Fact]
    public async Task Checkout_with_an_invalid_discount_returns_400()
    {
        var token = await CartWith("ceramic-mug", "MUG-CRM", 1);
        var response = await _client.PostAsJsonAsync("/api/checkout", Checkout(token, "NOPE"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Discount_preview_computes_the_amount_for_a_valid_code()
    {
        var preview = await _client.GetFromJsonAsync<DiscountPreviewDto>(
            "/api/storefront/discounts/WELCOME10?subtotal=100&currency=USD");
        Assert.True(preview!.Valid);
        Assert.Equal(10.00m, preview.Discount!.Amount);
    }

    [Fact]
    public async Task Discount_preview_flags_an_unknown_code()
    {
        var preview = await _client.GetFromJsonAsync<DiscountPreviewDto>("/api/storefront/discounts/DOESNOTEXIST");
        Assert.False(preview!.Valid);
        Assert.NotNull(preview.Reason);
    }

    [Fact]
    public async Task Admin_can_list_and_fetch_a_placed_order()
    {
        var token = await CartWith("canvas-tote", "TOTE-OLV", 1);
        var placed = await (await _client.PostAsJsonAsync("/api/checkout", Checkout(token)))
            .Content.ReadFromJsonAsync<OrderDto>();

        var list = await _client.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/api/admin/orders");
        Assert.Contains(list!.Items, o => o.Number == placed!.Number);

        var detail = await _client.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{placed!.Id}");
        Assert.Equal(placed.Number, detail!.Number);
    }

    [Fact]
    public async Task Fulfilling_then_refunding_an_order_follows_the_lifecycle_and_restocks()
    {
        var startStock = await Available("leather-belt", "BELT-34");
        var token = await CartWith("leather-belt", "BELT-34", 2);
        var order = await (await _client.PostAsJsonAsync("/api/checkout", Checkout(token)))
            .Content.ReadFromJsonAsync<OrderDto>();

        Assert.Equal(startStock - 2, await Available("leather-belt", "BELT-34"));

        // Fulfillment is driven by shipments: a full shipment moves the order to Fulfilled.
        var shipment = await _client.PostAsJsonAsync($"/api/admin/orders/{order!.Id}/shipments",
            new CreateShipmentRequest
            {
                Carrier = "UPS",
                TrackingNumber = "1Z-TEST",
                Lines = order.Items.Select(li => new CreateShipmentLineInput { OrderLineItemId = li.Id, Quantity = li.Quantity }).ToList(),
            });
        Assert.Equal(HttpStatusCode.Created, shipment.StatusCode);
        var shipped = await shipment.Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Fulfilled", shipped!.Status);

        var refunded = await (await _client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/transition",
            new TransitionOrderRequest { Status = "Refunded" })).Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Refunded", refunded!.Status);

        // Refund restores stock.
        Assert.Equal(startStock, await Available("leather-belt", "BELT-34"));
    }

    [Fact]
    public async Task Illegal_order_transition_returns_409()
    {
        var token = await CartWith("ceramic-mug", "MUG-SLT", 1);
        var order = await (await _client.PostAsJsonAsync("/api/checkout", Checkout(token)))
            .Content.ReadFromJsonAsync<OrderDto>();

        var response = await _client.PostAsJsonAsync($"/api/admin/orders/{order!.Id}/transition",
            new TransitionOrderRequest { Status = "Pending" }); // Paid -> Pending is illegal
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Admin_discount_codes_can_be_listed_created_and_deleted()
    {
        var seeded = await _client.GetFromJsonAsync<List<DiscountDto>>("/api/admin/discounts");
        Assert.True(seeded!.Count >= 2);

        var create = await _client.PostAsJsonAsync("/api/admin/discounts",
            new CreateDiscountRequest { Code = "SAVE20", Type = "Percentage", Value = 20m, UsageLimit = 50 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<DiscountDto>();
        Assert.Equal("SAVE20", created!.Code);

        var delete = await _client.DeleteAsync($"/api/admin/discounts/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Percentage_discount_over_100_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/discounts",
            new CreateDiscountRequest { Code = "TOOBIG", Type = "Percentage", Value = 150m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
