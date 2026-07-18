using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

/// <summary>
/// The order lifecycle guardrails as seen through the admin transition endpoint: every illegal manual
/// move is a 409 ProblemDetails with the right title, unknown targets are validation errors, and a fully
/// redeemed gift card cannot be spent a second time.
/// </summary>
public class OrderStateMachineTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public OrderStateMachineTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<OrderDto> PlaceOrder(string slug, string sku, int qty = 1, string? giftCard = null)
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        var variantId = product!.Variants.Single(v => v.Sku == sku).Id;
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            GiftCardCode = giftCard,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        var response = await _client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private Task<HttpResponseMessage> Transition(Guid orderId, string status) =>
        _admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/transition", new TransitionOrderRequest { Status = status });

    private static async Task AssertInvalidTransition(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid transition", doc.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("Pending")]    // paid orders never move back to pending
    [InlineData("Fulfilled")]  // fulfillment is shipment-derived, never a manual move
    public async Task An_illegal_move_out_of_paid_is_a_409_problem(string target)
    {
        var order = await PlaceOrder("ceramic-mug", "MUG-CRM");
        await AssertInvalidTransition(await Transition(order.Id, target));
    }

    [Fact]
    public async Task Nothing_can_move_out_of_a_cancelled_order()
    {
        var order = await PlaceOrder("ceramic-mug", "MUG-SLT");
        Assert.Equal(HttpStatusCode.OK, (await Transition(order.Id, "Cancelled")).StatusCode);

        await AssertInvalidTransition(await Transition(order.Id, "Paid"));
        await AssertInvalidTransition(await Transition(order.Id, "Refunded"));
    }

    [Fact]
    public async Task Nothing_can_move_out_of_a_refunded_order()
    {
        var order = await PlaceOrder("canvas-tote", "TOTE-OLV");
        Assert.Equal(HttpStatusCode.OK, (await Transition(order.Id, "Refunded")).StatusCode);

        await AssertInvalidTransition(await Transition(order.Id, "Cancelled"));
    }

    [Fact]
    public async Task An_unknown_target_status_is_a_validation_error()
    {
        var order = await PlaceOrder("classic-tee", "TEE-M-BLK");
        var response = await Transition(order.Id, "Teleported");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Transitioning_an_unknown_order_is_a_404()
    {
        var response = await Transition(Guid.NewGuid(), "Cancelled");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_fully_redeemed_gift_card_cannot_be_redeemed_a_second_time()
    {
        // A $10 card applied to a larger order is spent to a zero balance in one checkout.
        var code = (await (await _admin.PostAsJsonAsync("/api/admin/gift-cards", new IssueGiftCardRequest { Amount = 10m }))
            .Content.ReadFromJsonAsync<GiftCardDto>())!.Code;

        var first = await PlaceOrder("ceramic-mug", "MUG-CRM", 1, giftCard: code);
        Assert.Equal(10m, first.GiftCardTotal.Amount);

        var balance = await _client.GetFromJsonAsync<GiftCardBalanceDto>($"/api/storefront/gift-cards/{code}");
        Assert.False(balance!.Valid); // exhausted

        // A second checkout with the same (now empty) card is rejected before payment.
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await _client.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/ceramic-mug");
        var variantId = product!.Variants.Single(v => v.Sku == "MUG-CRM").Id;
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = 1 });
        var response = await _client.PostAsJsonAsync("/api/checkout", new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            GiftCardCode = code,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // InvalidGiftCard
    }
}
