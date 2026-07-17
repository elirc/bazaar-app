using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class HardeningTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public HardeningTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _client.AuthenticateAdminAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> VariantId(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<int> Available(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Available;
    }

    private async Task<string> CartWith(string slug, string sku, int quantity)
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(slug, sku);
        await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = variantId, Quantity = quantity });
        return cart.Token;
    }

    private static CheckoutRequest Checkout(string token) => new()
    {
        CartToken = token,
        Email = "buyer@example.com",
        ShippingAddress = new AddressInput
        {
            Name = "Edith Clarke", Line1 = "1 Grid Ave", City = "Denver", PostalCode = "80202", Country = "US",
        },
    };

    [Fact]
    public async Task Buying_the_last_units_drives_availability_to_zero_and_blocks_the_next_order()
    {
        var stock = await Available("wool-blanket", "BLNK-OAT"); // seeded at 8

        var firstToken = await CartWith("wool-blanket", "BLNK-OAT", stock);
        var first = await _client.PostAsJsonAsync("/api/checkout", Checkout(firstToken));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(0, await Available("wool-blanket", "BLNK-OAT"));

        var secondToken = await CartWith("wool-blanket", "BLNK-OAT", 1);
        var second = await _client.PostAsJsonAsync("/api/checkout", Checkout(secondToken));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Pagination_past_the_last_page_returns_no_items_but_keeps_totals()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>(
            "/api/storefront/products?page=99&pageSize=5");

        Assert.Empty(page!.Items);
        Assert.Equal(6, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(99, page.Page);
    }

    [Fact]
    public async Task Pagination_clamps_an_oversized_page_size()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>(
            "/api/storefront/products?pageSize=1000");
        Assert.Equal(60, page!.PageSize); // storefront max
    }

    [Fact]
    public async Task Error_responses_use_the_problem_details_content_type()
    {
        var mugId = await VariantId("ceramic-mug", "MUG-CRM");
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();

        var response = await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = mugId, Quantity = 100 }); // exceeds per-line max

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Adding_zero_quantity_is_rejected_by_validation()
    {
        var mugId = await VariantId("ceramic-mug", "MUG-CRM");
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();

        var response = await _client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = mugId, Quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_with_a_malformed_email_is_rejected()
    {
        var token = await CartWith("ceramic-mug", "MUG-CRM", 1);
        var request = Checkout(token) with { Email = "not-an-email" };

        var response = await _client.PostAsJsonAsync("/api/checkout", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_admin_order_returns_404()
    {
        var response = await _client.GetAsync($"/api/admin/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Removing_a_missing_cart_line_is_idempotent()
    {
        var cart = await (await _client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var response = await _client.DeleteAsync($"/api/cart/{cart!.Token}/items/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CartDto>();
        Assert.Empty(updated!.Items);
    }
}
