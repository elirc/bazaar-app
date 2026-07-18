using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class TaxZonesAndGiftCardsTests : IClassFixture<BazaarApiFactory>
{
    private readonly HttpClient _client;
    private readonly BazaarApiFactory _factory;

    public TaxZonesAndGiftCardsTests(BazaarApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> VariantId(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<OrderDto> Purchase(string slug, string sku, int qty, string? region = null, string? giftCard = null, string email = "buyer@example.com")
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(slug, sku);
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = email,
            GiftCardCode = giftCard,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "City", Region = region, PostalCode = "00000", Country = "US" },
        };
        var response = await _client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    [Fact]
    public async Task A_region_zone_applies_its_standard_rate()
    {
        var order = await Purchase("ceramic-mug", "MUG-CRM", 2, region: "CA"); // 28 * 9.5%
        Assert.Equal(2.66m, order.TaxTotal.Amount);
    }

    [Fact]
    public async Task A_zero_tax_region_charges_no_tax()
    {
        var order = await Purchase("ceramic-mug", "MUG-SLT", 1, region: "OR");
        Assert.Equal(0m, order.TaxTotal.Amount);
    }

    [Fact]
    public async Task A_region_less_address_falls_back_to_the_flat_rate()
    {
        var order = await Purchase("ceramic-mug", "MUG-CRM", 2, region: null); // fallback 8.25%
        Assert.Equal(2.31m, order.TaxTotal.Amount);
    }

    [Fact]
    public async Task A_products_tax_category_can_be_exempt_in_a_zone()
    {
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var slug = $"food-{Guid.NewGuid():N}".Substring(0, 18);
        var create = await admin.PostAsJsonAsync("/api/admin/products", new CreateProductRequest
        {
            Title = "Groceries",
            Slug = slug,
            Status = "Active",
            TaxCategory = "food",
            Images = new() { new ImageInput { Url = "https://images.bazaar.test/x.jpg", Position = 0 } },
            Variants = new() { new VariantInput { Sku = $"FOOD-{Guid.NewGuid():N}".Substring(0, 12), Price = 10m, StockOnHand = 5 } },
        });
        var product = await create.Content.ReadFromJsonAsync<ProductDetailDto>();
        Assert.Equal("food", product!.TaxCategory);

        var order = await Purchase(slug, product.Variants[0].Sku, 1, region: "CA"); // food exempt in CA
        Assert.Equal(0m, order.TaxTotal.Amount);
    }

    [Fact]
    public async Task Admin_issues_a_gift_card_and_its_balance_is_publicly_checkable()
    {
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var card = await (await admin.PostAsJsonAsync("/api/admin/gift-cards", new IssueGiftCardRequest { Amount = 40m }))
            .Content.ReadFromJsonAsync<GiftCardDto>();
        Assert.Equal(40m, card!.Balance.Amount);

        var balance = await _client.GetFromJsonAsync<GiftCardBalanceDto>($"/api/storefront/gift-cards/{card.Code}");
        Assert.True(balance!.Valid);
        Assert.Equal(40m, balance.Balance!.Amount);

        var unknown = await _client.GetFromJsonAsync<GiftCardBalanceDto>("/api/storefront/gift-cards/NOPE");
        Assert.False(unknown!.Valid);
    }

    [Fact]
    public async Task A_gift_card_is_tendered_after_discount_tax_and_shipping()
    {
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var card = await (await admin.PostAsJsonAsync("/api/admin/gift-cards", new IssueGiftCardRequest { Amount = 10m }))
            .Content.ReadFromJsonAsync<GiftCardDto>();

        var order = await Purchase("ceramic-mug", "MUG-CRM", 2, region: null, giftCard: card!.Code); // grand 36.30
        Assert.Equal(36.30m, order.GrandTotal.Amount);
        Assert.Equal(10m, order.GiftCardTotal.Amount);
        Assert.Equal(card.Code, order.GiftCardCode);

        // The card is fully spent.
        var balance = await _client.GetFromJsonAsync<GiftCardBalanceDto>($"/api/storefront/gift-cards/{card.Code}");
        Assert.False(balance!.Valid);
    }

    [Fact]
    public async Task A_gift_card_covering_the_whole_total_skips_the_payment_gateway()
    {
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var card = await (await admin.PostAsJsonAsync("/api/admin/gift-cards", new IssueGiftCardRequest { Amount = 100m }))
            .Content.ReadFromJsonAsync<GiftCardDto>();

        // "decline" email would fail a card charge, but the gateway is skipped when fully covered.
        var order = await Purchase("ceramic-mug", "MUG-CRM", 2, region: null, giftCard: card!.Code, email: "decline@example.com");
        Assert.Equal("Paid", order.Status);
        Assert.Equal(order.GrandTotal.Amount, order.GiftCardTotal.Amount);
    }

    [Fact]
    public async Task An_invalid_gift_card_code_is_rejected()
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId("ceramic-mug", "MUG-CRM");
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = 1 });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            GiftCardCode = "DOESNOTEXIST",
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "City", PostalCode = "00000", Country = "US" },
        };
        var response = await _client.PostAsJsonAsync("/api/checkout", checkout);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
