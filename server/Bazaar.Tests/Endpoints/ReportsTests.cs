using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class ReportsTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public ReportsTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<OrderDto> Purchase(string slug, string sku, int qty, string? discount = null)
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        var variantId = product!.Variants.Single(v => v.Sku == sku).Id;
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items", new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "buyer@example.com",
            DiscountCode = discount,
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        var response = await _client.PostAsJsonAsync("/api/checkout", checkout);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    [Fact]
    public async Task Reports_require_admin()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/admin/reports/sales")).StatusCode);
    }

    [Fact]
    public async Task Sales_report_aggregates_placed_orders()
    {
        await Purchase("ceramic-mug", "MUG-CRM", 2);
        var report = await _admin.GetFromJsonAsync<SalesReportDto>("/api/admin/reports/sales");
        Assert.True(report!.TotalOrders >= 1);
        Assert.True(report.TotalRevenue.Amount > 0);
        Assert.NotEmpty(report.Buckets);
    }

    [Fact]
    public async Task Top_products_ranks_by_quantity_sold()
    {
        await Purchase("canvas-tote", "TOTE-NAT", 3);
        var top = await _admin.GetFromJsonAsync<List<TopProductDto>>("/api/admin/reports/top-products?limit=20");
        Assert.Contains(top!, p => p.Sku == "TOTE-NAT" && p.QuantitySold >= 3);
    }

    [Fact]
    public async Task Low_stock_lists_variants_at_or_below_the_threshold()
    {
        // wool-blanket BLNK-OAT is seeded at 8 on hand.
        var low = await _admin.GetFromJsonAsync<List<LowStockDto>>("/api/admin/reports/low-stock?threshold=10");
        Assert.Contains(low!, l => l.Sku == "BLNK-OAT");
    }

    [Fact]
    public async Task Discount_usage_reflects_redeemed_codes()
    {
        await Purchase("ceramic-mug", "MUG-SLT", 1, "WELCOME10");
        var usage = await _admin.GetFromJsonAsync<List<DiscountUsageDto>>("/api/admin/reports/discounts");
        Assert.Contains(usage!, d => d.Code == "WELCOME10" && d.TimesUsed >= 1);
    }
}
