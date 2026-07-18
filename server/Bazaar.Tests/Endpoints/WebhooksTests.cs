using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class WebhooksTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public WebhooksTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<WebhookSubscriptionDto> Subscribe(string url, params string[] events)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/webhooks",
            new CreateWebhookRequest { Url = url, Events = events.ToList() });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WebhookSubscriptionDto>())!;
    }

    private Task<List<WebhookDeliveryDto>?> Deliveries(Guid subscriptionId) =>
        _admin.GetFromJsonAsync<List<WebhookDeliveryDto>>($"/api/admin/webhooks/deliveries?subscriptionId={subscriptionId}");

    private async Task<OrderDto> Purchase(string slug, string sku, int qty = 1)
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        var variantId = product!.Variants.Single(v => v.Sku == sku).Id;
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        var response = await _client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    [Fact]
    public async Task Webhook_admin_requires_admin()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/webhooks")).StatusCode);
    }

    [Fact]
    public async Task Subscribing_to_an_unknown_event_is_rejected()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/webhooks",
            new CreateWebhookRequest { Url = "https://hooks.example.com/x", Events = new() { "order.exploded" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_fires_order_created_and_paid_to_a_subscriber()
    {
        var sub = await Subscribe("https://hooks.example.com/ok", "order.created", "order.paid", "order.fulfilled");
        Assert.False(string.IsNullOrWhiteSpace(sub.Secret));

        await Purchase("ceramic-mug", "MUG-CRM");

        var deliveries = await Deliveries(sub.Id);
        Assert.Equal(2, deliveries!.Count);
        Assert.Contains(deliveries, d => d.Event == "order.created" && d.Success && d.AttemptCount == 1);
        Assert.Contains(deliveries, d => d.Event == "order.paid" && d.Success && d.AttemptCount == 1);
    }

    [Fact]
    public async Task Only_subscribed_events_are_delivered()
    {
        var sub = await Subscribe("https://hooks.example.com/ok2", "order.paid");
        await Purchase("classic-tee", "TEE-M-BLK");

        var deliveries = await Deliveries(sub.Id);
        Assert.Single(deliveries!);
        Assert.Equal("order.paid", deliveries![0].Event);
    }

    [Fact]
    public async Task A_failing_endpoint_is_retried_up_to_the_cap()
    {
        var sub = await Subscribe("https://hooks.example.com/fail-here", "order.created");
        await Purchase("leather-belt", "BELT-32");

        var deliveries = await Deliveries(sub.Id);
        Assert.Single(deliveries!);
        Assert.False(deliveries![0].Success);
        Assert.Equal(3, deliveries[0].AttemptCount); // WebhookDispatcher.MaxAttempts
    }

    [Fact]
    public async Task Full_shipment_fires_order_fulfilled()
    {
        var sub = await Subscribe("https://hooks.example.com/fulfil", "order.fulfilled");
        var order = await Purchase("ceramic-mug", "MUG-CRM", 1);

        await _admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/shipments", new CreateShipmentRequest
        {
            Carrier = "UPS",
            TrackingNumber = "1Z-1",
            Lines = new() { new CreateShipmentLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 1 } },
        });

        var deliveries = await Deliveries(sub.Id);
        Assert.Contains(deliveries!, d => d.Event == "order.fulfilled" && d.Success);
    }

    [Fact]
    public async Task Refunding_fires_order_refunded()
    {
        var sub = await Subscribe("https://hooks.example.com/refund", "order.refunded");
        var order = await Purchase("canvas-tote", "TOTE-OLV", 1);

        await _admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/transition", new TransitionOrderRequest { Status = "Refunded" });

        var deliveries = await Deliveries(sub.Id);
        Assert.Contains(deliveries!, d => d.Event == "order.refunded" && d.Success);
    }

    [Fact]
    public async Task Subscriptions_can_be_listed_and_deleted()
    {
        var sub = await Subscribe("https://hooks.example.com/crud", "order.paid");
        var list = await _admin.GetFromJsonAsync<List<WebhookSubscriptionDto>>("/api/admin/webhooks");
        Assert.Contains(list!, s => s.Id == sub.Id);

        var delete = await _admin.DeleteAsync($"/api/admin/webhooks/{sub.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
