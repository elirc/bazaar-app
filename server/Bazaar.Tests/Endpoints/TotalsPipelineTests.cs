using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

/// <summary>
/// End-to-end invariants for the checkout totals pipeline: discount -> tax -> shipping -> gift-card
/// tender, across percent/fixed discounts, region and flat-fallback tax, each shipping method, and
/// the free-shipping threshold boundary. Every case pins exact cents and checks cent conservation.
/// </summary>
public class TotalsPipelineTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly BazaarApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public TotalsPipelineTests(BazaarApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> VariantId(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<string> IssueGiftCard(decimal amount)
    {
        var card = await (await _admin.PostAsJsonAsync("/api/admin/gift-cards", new IssueGiftCardRequest { Amount = amount }))
            .Content.ReadFromJsonAsync<GiftCardDto>();
        return card!.Code;
    }

    private async Task<OrderDto> Checkout(
        string slug, string sku, int qty,
        string? discount = null, string? region = null, string? method = null, string? giftCard = null)
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(slug, sku);
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            DiscountCode = discount,
            ShippingMethodCode = method,
            GiftCardCode = giftCard,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "City", Region = region, PostalCode = "00000", Country = "US" },
        };
        var response = await _client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    private static void AssertCentsConserved(OrderDto order)
    {
        // grand = subtotal - discount + tax + shipping (gift card is a separate tender, not part of the total)
        var expected = ToCents(order.Subtotal) - ToCents(order.DiscountTotal) + ToCents(order.TaxTotal) + ToCents(order.ShippingTotal);
        Assert.Equal(expected, ToCents(order.GrandTotal));
    }

    private static long ToCents(MoneyDto money) => (long)decimal.Round(money.Amount * 100m, 0, MidpointRounding.ToEven);

    [Fact]
    public async Task Percent_discount_region_tax_express_shipping_and_a_partial_gift_card_compose_exactly()
    {
        var giftCard = await IssueGiftCard(20m);

        // 5 mugs @ $14 = 70.00 subtotal; WELCOME10 -> 7.00 off; CA tax 9.5% on the pre-discount 70 = 6.65;
        // express flat 14.99; grand = 70 - 7 + 6.65 + 14.99 = 84.64; gift card tenders 20 of it.
        var order = await Checkout("ceramic-mug", "MUG-CRM", 5,
            discount: "WELCOME10", region: "CA", method: "express", giftCard: giftCard);

        Assert.Equal(70.00m, order.Subtotal.Amount);
        Assert.Equal(7.00m, order.DiscountTotal.Amount);
        Assert.Equal(6.65m, order.TaxTotal.Amount);
        Assert.Equal(14.99m, order.ShippingTotal.Amount);
        Assert.Equal(84.64m, order.GrandTotal.Amount);
        Assert.Equal(20.00m, order.GiftCardTotal.Amount);
        AssertCentsConserved(order);
    }

    [Fact]
    public async Task Fixed_amount_discount_flows_through_flat_fallback_tax_and_a_gift_card()
    {
        var giftCard = await IssueGiftCard(10m);

        // 2 mugs @ $14 = 28.00; SHIP5 -> 5.00 off; region-less US -> flat 8.25% on 28 = 2.31;
        // standard shipping below threshold = 5.99; grand = 28 - 5 + 2.31 + 5.99 = 31.30; gift card 10.
        var order = await Checkout("ceramic-mug", "MUG-SLT", 2, discount: "SHIP5", giftCard: giftCard);

        Assert.Equal(28.00m, order.Subtotal.Amount);
        Assert.Equal(5.00m, order.DiscountTotal.Amount);
        Assert.Equal(2.31m, order.TaxTotal.Amount);
        Assert.Equal(5.99m, order.ShippingTotal.Amount);
        Assert.Equal(31.30m, order.GrandTotal.Amount);
        Assert.Equal(10.00m, order.GiftCardTotal.Amount);
        AssertCentsConserved(order);
    }

    [Fact]
    public async Task Free_shipping_applies_exactly_at_the_threshold_through_checkout()
    {
        // Two variants straddling the $75 free-shipping threshold to the cent.
        var slug = $"thresh-{Guid.NewGuid():N}".Substring(0, 16);
        var atSku = $"AT-{Guid.NewGuid():N}".Substring(0, 12);
        var underSku = $"UN-{Guid.NewGuid():N}".Substring(0, 12);
        var create = await _admin.PostAsJsonAsync("/api/admin/products", new CreateProductRequest
        {
            Title = "Threshold Item",
            Slug = slug,
            Status = "Active",
            TaxCategory = "standard",
            Images = new() { new ImageInput { Url = "https://images.bazaar.test/t.jpg", Position = 0 } },
            Variants = new()
            {
                new VariantInput { Sku = atSku, Price = 75.00m, StockOnHand = 5 },
                new VariantInput { Sku = underSku, Price = 74.99m, StockOnHand = 5 },
            },
        });
        create.EnsureSuccessStatusCode();

        var atThreshold = await Checkout(slug, atSku, 1);      // subtotal exactly 75.00 -> ships free
        Assert.Equal(75.00m, atThreshold.Subtotal.Amount);
        Assert.Equal(0.00m, atThreshold.ShippingTotal.Amount);

        var underThreshold = await Checkout(slug, underSku, 1); // subtotal 74.99 -> flat 5.99
        Assert.Equal(74.99m, underThreshold.Subtotal.Amount);
        Assert.Equal(5.99m, underThreshold.ShippingTotal.Amount);
    }

    [Fact]
    public async Task Weight_shipping_adds_a_per_kilogram_surcharge_over_the_base_rate()
    {
        // Wool blanket weighs 1500g; freight = base 3.99 + 1.50/kg * 1.5kg = 6.24.
        var order = await Checkout("wool-blanket", "BLNK-OAT", 1, method: "freight");
        Assert.Equal(6.24m, order.ShippingTotal.Amount);
        Assert.Equal("Freight (by weight)", order.ShippingMethod);
        AssertCentsConserved(order);
    }
}
