using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class ReturnsTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public ReturnsTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> NewCustomer()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"rma-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);
        return client;
    }

    private async Task<int> Available(HttpClient client, string slug, string sku)
    {
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Available;
    }

    private async Task<OrderDto> Purchase(HttpClient client, string slug, string sku, int qty, string? discount = null)
    {
        var cart = await (await client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        var variantId = product!.Variants.Single(v => v.Sku == sku).Id;
        await client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            DiscountCode = discount,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        var response = await client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private async Task Fulfill(HttpClient admin, Guid orderId)
    {
        // A full shipment drives the order to Fulfilled (fulfillment is shipment-derived).
        var order = await admin.GetFromJsonAsync<OrderDto>($"/api/admin/orders/{orderId}");
        var response = await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/shipments", new CreateShipmentRequest
        {
            Carrier = "UPS",
            TrackingNumber = "1Z-TEST",
            Lines = order!.Items.Select(li => new CreateShipmentLineInput { OrderLineItemId = li.Id, Quantity = li.Quantity }).ToList(),
        });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_return_on_a_paid_but_unfulfilled_order_is_rejected()
    {
        var customer = await NewCustomer();
        var order = await Purchase(customer, "canvas-tote", "TOTE-OLV", 1);

        var response = await customer.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Lines = new() { new CreateReturnLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 1 } } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_customer_cannot_open_a_return_on_another_customers_order()
    {
        var alice = await NewCustomer();
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var order = await Purchase(alice, "leather-belt", "BELT-32", 1);
        await Fulfill(admin, order.Id);

        var bob = await NewCustomer();
        var response = await bob.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Lines = new() { new CreateReturnLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 1 } } });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Requesting_more_than_was_ordered_is_blocked()
    {
        var customer = await NewCustomer();
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var order = await Purchase(customer, "canvas-tote", "TOTE-NAT", 2);
        await Fulfill(admin, order.Id);

        var response = await customer.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Lines = new() { new CreateReturnLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 3 } } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Approving_a_return_issues_a_discount_and_tax_adjusted_refund_and_restocks()
    {
        var customer = await NewCustomer();
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();

        var before = await Available(customer, "ceramic-mug", "MUG-CRM");
        var order = await Purchase(customer, "ceramic-mug", "MUG-CRM", 2, "WELCOME10"); // $28, 10% off
        Assert.Equal(before - 2, await Available(customer, "ceramic-mug", "MUG-CRM"));
        await Fulfill(admin, order.Id);

        var created = await (await customer.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Reason = "Changed my mind", Lines = new() { new CreateReturnLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 2 } } }))
            .Content.ReadFromJsonAsync<ReturnRequestDto>();
        Assert.Equal("Requested", created!.Status);

        // Admin sees it in the queue and approves.
        var queue = await admin.GetFromJsonAsync<PagedResult<AdminReturnDto>>("/api/admin/returns?status=Requested");
        Assert.Contains(queue!.Items, r => r.Id == created.Id);

        var approved = await (await admin.PostAsync($"/api/admin/returns/{created.Id}/approve", null))
            .Content.ReadFromJsonAsync<AdminReturnDto>();
        Assert.Equal("Approved", approved!.Status);
        // 28 - 2.80 discount share + 2.31 tax share = 27.51
        Assert.Equal(27.51m, approved.RefundAmount.Amount);

        // Stock restored.
        Assert.Equal(before, await Available(customer, "ceramic-mug", "MUG-CRM"));
    }

    [Fact]
    public async Task A_return_can_be_rejected_and_does_not_refund()
    {
        var customer = await NewCustomer();
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var order = await Purchase(customer, "leather-belt", "BELT-34", 1);
        await Fulfill(admin, order.Id);

        var created = await (await customer.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Lines = new() { new CreateReturnLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 1 } } }))
            .Content.ReadFromJsonAsync<ReturnRequestDto>();

        var rejected = await (await admin.PostAsJsonAsync($"/api/admin/returns/{created!.Id}/reject",
            new RejectReturnRequest { Reason = "Outside the return window" })).Content.ReadFromJsonAsync<AdminReturnDto>();
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal(0m, rejected.RefundAmount.Amount);

        // Approving after a decision is a conflict.
        var again = await admin.PostAsync($"/api/admin/returns/{created.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task The_admin_returns_queue_requires_admin()
    {
        var customer = await NewCustomer();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/returns")).StatusCode);
    }

    [Fact]
    public async Task Customers_see_their_own_returns()
    {
        var customer = await NewCustomer();
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var order = await Purchase(customer, "ceramic-mug", "MUG-SLT", 1);
        await Fulfill(admin, order.Id);
        await customer.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Lines = new() { new CreateReturnLineInput { OrderLineItemId = order.Items[0].Id, Quantity = 1 } } });

        var mine = await customer.GetFromJsonAsync<List<ReturnRequestDto>>("/api/account/returns");
        Assert.Contains(mine!, r => r.OrderId == order.Id);
    }
}
