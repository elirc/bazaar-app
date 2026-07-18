using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

/// <summary>
/// Return/refund correctness at the edges: the over-refund guard at its exact boundary, restock
/// arithmetic on a partial return, the fulfillment precondition, and tender-aware refunds that restore
/// a gift card instead of over-refunding real money to the card.
/// </summary>
public class RefundScenariosTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public RefundScenariosTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> NewCustomer()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"refund-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);
        return client;
    }

    private async Task<HttpClient> Admin()
    {
        var client = _factory.CreateClient();
        await client.AuthenticateAdminAsync();
        return client;
    }

    private async Task<int> Available(HttpClient client, string slug, string sku)
    {
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Available;
    }

    private async Task<OrderDto> Purchase(HttpClient client, string slug, string sku, int qty, string? giftCard = null)
    {
        var cart = await (await client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        var variantId = product!.Variants.Single(v => v.Sku == sku).Id;
        await client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            GiftCardCode = giftCard,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        var response = await client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private static async Task<HttpResponseMessage> Ship(HttpClient admin, OrderDto order, params (Guid lineId, int qty)[] lines) =>
        await admin.PostAsJsonAsync($"/api/admin/orders/{order.Id}/shipments", new CreateShipmentRequest
        {
            Carrier = "UPS",
            TrackingNumber = $"1Z-{Guid.NewGuid():N}".Substring(0, 12),
            Lines = lines.Select(l => new CreateShipmentLineInput { OrderLineItemId = l.lineId, Quantity = l.qty }).ToList(),
        });

    private static Task<HttpResponseMessage> RequestReturn(HttpClient customer, OrderDto order, Guid lineId, int qty) =>
        customer.PostAsJsonAsync($"/api/account/orders/{order.Id}/returns",
            new CreateReturnRequest { Lines = new() { new CreateReturnLineInput { OrderLineItemId = lineId, Quantity = qty } } });

    [Fact]
    public async Task Returning_up_to_the_exact_ordered_quantity_succeeds_then_one_more_is_blocked()
    {
        var customer = await NewCustomer();
        var admin = await Admin();
        var order = await Purchase(customer, "ceramic-mug", "MUG-CRM", 3);
        var lineId = order.Items[0].Id;
        (await Ship(admin, order, (lineId, 3))).EnsureSuccessStatusCode(); // Fulfilled

        var first = await RequestReturn(customer, order, lineId, 2);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var atBoundary = await RequestReturn(customer, order, lineId, 1); // 2 + 1 == exactly 3
        Assert.Equal(HttpStatusCode.Created, atBoundary.StatusCode);

        var overBoundary = await RequestReturn(customer, order, lineId, 1); // 3 + 1 > 3
        Assert.Equal(HttpStatusCode.Conflict, overBoundary.StatusCode);
        Assert.Equal("application/problem+json", overBoundary.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_partial_return_restocks_only_the_returned_quantity()
    {
        var customer = await NewCustomer();
        var admin = await Admin();

        var before = await Available(customer, "ceramic-mug", "MUG-CRM");
        var order = await Purchase(customer, "ceramic-mug", "MUG-CRM", 3);
        Assert.Equal(before - 3, await Available(customer, "ceramic-mug", "MUG-CRM"));

        var lineId = order.Items[0].Id;
        (await Ship(admin, order, (lineId, 3))).EnsureSuccessStatusCode();

        var created = await (await RequestReturn(customer, order, lineId, 2)).Content.ReadFromJsonAsync<ReturnRequestDto>();
        (await admin.PostAsync($"/api/admin/returns/{created!.Id}/approve", null)).EnsureSuccessStatusCode();

        // Sold 3, returned 2 -> net 1 gone from the original stock.
        Assert.Equal(before - 1, await Available(customer, "ceramic-mug", "MUG-CRM"));
    }

    [Fact]
    public async Task A_partially_fulfilled_order_cannot_be_returned()
    {
        var customer = await NewCustomer();
        var admin = await Admin();
        var order = await Purchase(customer, "ceramic-mug", "MUG-CRM", 2);
        var lineId = order.Items[0].Id;

        var partial = await (await Ship(admin, order, (lineId, 1))).Content.ReadFromJsonAsync<OrderDto>();
        Assert.Equal("PartiallyFulfilled", partial!.Status);

        var response = await RequestReturn(customer, order, lineId, 1);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // only fully fulfilled orders are returnable
    }

    [Fact]
    public async Task A_gift_card_funded_order_return_refunds_the_gift_card_instead_of_over_refunding_the_card()
    {
        var admin = await Admin();
        var customer = await NewCustomer();

        // A $100 card wholly covers the $36.30 order (28 + 2.31 tax + 5.99 shipping); the card is charged $0.
        var code = (await (await admin.PostAsJsonAsync("/api/admin/gift-cards", new IssueGiftCardRequest { Amount = 100m }))
            .Content.ReadFromJsonAsync<GiftCardDto>())!.Code;

        var order = await Purchase(customer, "ceramic-mug", "MUG-CRM", 2, giftCard: code);
        Assert.Equal(36.30m, order.GrandTotal.Amount);
        Assert.Equal(36.30m, order.GiftCardTotal.Amount); // whole total tendered by the card

        var afterCheckout = await customer.GetFromJsonAsync<GiftCardBalanceDto>($"/api/storefront/gift-cards/{code}");
        Assert.Equal(63.70m, afterCheckout!.Balance!.Amount); // 100 - 36.30 redeemed

        var lineId = order.Items[0].Id;
        (await Ship(admin, order, (lineId, 2))).EnsureSuccessStatusCode();

        var created = await (await RequestReturn(customer, order, lineId, 2)).Content.ReadFromJsonAsync<ReturnRequestDto>();
        var approved = await (await admin.PostAsync($"/api/admin/returns/{created!.Id}/approve", null))
            .Content.ReadFromJsonAsync<AdminReturnDto>();

        // Discount/tax-adjusted refund (28 + 2.31 tax, shipping excluded) = 30.31, all restored to the gift card.
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal(30.31m, approved.RefundAmount.Amount);

        var afterRefund = await customer.GetFromJsonAsync<GiftCardBalanceDto>($"/api/storefront/gift-cards/{code}");
        Assert.True(afterRefund!.Valid);
        Assert.Equal(63.70m + 30.31m, afterRefund.Balance!.Amount); // 94.01 back on the card, not double-refunded to a card
    }
}
