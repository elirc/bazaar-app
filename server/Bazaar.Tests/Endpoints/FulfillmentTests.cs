using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class FulfillmentTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public FulfillmentTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<OrderDto> PlaceOrder(string slug, string sku, int qty)
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

    private Task<HttpResponseMessage> Ship(Guid orderId, Guid lineId, int qty, string carrier = "UPS", string tracking = "1Z-1") =>
        _admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/shipments", new CreateShipmentRequest
        {
            Carrier = carrier,
            TrackingNumber = tracking,
            Lines = new() { new CreateShipmentLineInput { OrderLineItemId = lineId, Quantity = qty } },
        });

    [Fact]
    public async Task A_partial_shipment_marks_the_order_partially_fulfilled_then_full_completes_it()
    {
        var order = await PlaceOrder("ceramic-mug", "MUG-CRM", 2);
        var lineId = order.Items[0].Id;

        var first = await (await Ship(order.Id, lineId, 1, tracking: "1Z-AAA")).Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("PartiallyFulfilled", first!.Status);
        Assert.Single(first.Shipments);
        Assert.Equal("1Z-AAA", first.Shipments[0].TrackingNumber);

        var second = await (await Ship(order.Id, lineId, 1, tracking: "1Z-BBB")).Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("Fulfilled", second!.Status);
        Assert.Equal(2, second.Shipments.Count);
    }

    [Fact]
    public async Task Shipping_more_than_was_ordered_is_blocked()
    {
        var order = await PlaceOrder("canvas-tote", "TOTE-NAT", 2);
        var response = await Ship(order.Id, order.Items[0].Id, 3);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancellation_is_blocked_once_the_order_has_shipped()
    {
        var order = await PlaceOrder("leather-belt", "BELT-32", 2);
        var shipped = await (await Ship(order.Id, order.Items[0].Id, 1)).Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("PartiallyFulfilled", shipped!.Status);

        var cancel = await _admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/transition",
            new TransitionOrderRequest { Status = "Cancelled" });
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }

    [Fact]
    public async Task A_cancelled_order_cannot_be_shipped()
    {
        var order = await PlaceOrder("canvas-tote", "TOTE-OLV", 1);
        var cancel = await _admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/transition",
            new TransitionOrderRequest { Status = "Cancelled" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var ship = await Ship(order.Id, order.Items[0].Id, 1);
        Assert.Equal(HttpStatusCode.Conflict, ship.StatusCode);
    }

    [Fact]
    public async Task Shipment_tracking_is_visible_on_the_public_order()
    {
        var order = await PlaceOrder("ceramic-mug", "MUG-SLT", 1);
        await Ship(order.Id, order.Items[0].Id, 1, carrier: "FedEx", tracking: "FX-999");

        var fetched = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        Assert.Single(fetched!.Shipments);
        Assert.Equal("FedEx", fetched.Shipments[0].Carrier);
        Assert.Equal("FX-999", fetched.Shipments[0].TrackingNumber);
    }

    [Fact]
    public async Task Creating_a_shipment_requires_admin()
    {
        var order = await PlaceOrder("classic-tee", "TEE-M-BLK", 1);

        // The guest client has no admin token.
        var forbidden = await _client.PostAsJsonAsync($"/api/admin/orders/{order.Id}/shipments", new CreateShipmentRequest
        {
            Carrier = "UPS", TrackingNumber = "X", Lines = new() { new CreateShipmentLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
    }
}
