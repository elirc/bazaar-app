using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;

namespace Bazaar.Tests.Endpoints;

public class ProductionReadinessTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly BazaarApiFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _admin;

    public ProductionReadinessTests(BazaarApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _admin = factory.CreateClient();
    }

    public Task InitializeAsync() => _admin.AuthenticateAdminAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Health_reports_a_structured_database_probe()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("bazaar-api", root.GetProperty("service").GetString());
        Assert.Equal("ok", root.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Admin_product_listing_clamps_an_oversized_page_size()
    {
        var page = await _admin.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/admin/products?pageSize=1000");
        Assert.Equal(100, page!.PageSize); // admin max
    }

    [Fact]
    public async Task Admin_order_listing_clamps_an_oversized_page_size()
    {
        var page = await _admin.GetFromJsonAsync<PagedResult<OrderSummaryDto>>("/api/admin/orders?pageSize=1000");
        Assert.Equal(100, page!.PageSize);
    }

    [Fact]
    public async Task Checkout_is_rate_limited_beyond_the_permitted_window()
    {
        // Boot a variant of the app with a low checkout permit limit, isolated to this test.
        var limitedFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:CheckoutPermitLimit", "3"));
        var limited = limitedFactory.CreateClient();

        var request = new CheckoutRequest
        {
            CartToken = "no-such-cart",
            Email = "buyer@example.com",
            ShippingAddress = new AddressInput { Name = "B", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await limited.PostAsJsonAsync("/api/checkout", request);
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
