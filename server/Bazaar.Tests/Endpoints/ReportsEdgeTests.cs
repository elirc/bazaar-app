using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

/// <summary>
/// Report edges against a freshly seeded store with no orders yet: empty aggregates return well-formed,
/// zeroed payloads (never a 500), and low-stock still surfaces genuinely low seeded inventory.
/// This class never places an order, so its shared database stays order-free.
/// </summary>
public class ReportsEdgeTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _admin;

    public ReportsEdgeTests(BazaarApiFactory factory)
    {
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Sales_report_is_empty_and_zeroed_before_any_order_is_placed()
    {
        var report = await _admin.GetFromJsonAsync<SalesReportDto>("/api/admin/reports/sales");
        Assert.Empty(report!.Buckets);
        Assert.Equal(0, report.TotalOrders);
        Assert.Equal(0m, report.TotalRevenue.Amount);
    }

    [Fact]
    public async Task Top_products_is_empty_before_any_sale()
    {
        var top = await _admin.GetFromJsonAsync<List<TopProductDto>>("/api/admin/reports/top-products");
        Assert.Empty(top!);
    }

    [Fact]
    public async Task Discount_usage_lists_seeded_codes_with_zero_uses()
    {
        var usage = await _admin.GetFromJsonAsync<List<DiscountUsageDto>>("/api/admin/reports/discounts");
        Assert.All(usage!, d => Assert.Equal(0, d.TimesUsed));
        Assert.Contains(usage!, d => d.Code == "WELCOME10");
    }

    [Fact]
    public async Task Low_stock_surfaces_the_genuinely_low_seeded_variant()
    {
        // The wool blanket is seeded at 8 on hand, under the default threshold of 10.
        var low = await _admin.GetFromJsonAsync<List<LowStockDto>>("/api/admin/reports/low-stock");
        Assert.Contains(low!, s => s.Sku == "BLNK-OAT" && s.Available == 8);
    }
}
