using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class AdminCatalogTests : IClassFixture<BazaarApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;

    public AdminCatalogTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _client.AuthenticateAdminAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static CreateProductRequest NewProduct(string slug, string sku, decimal price = 29.99m) => new()
    {
        Title = $"Test {slug}",
        Slug = slug,
        Description = "A product created by a test.",
        Status = "Active",
        Images = new() { new ImageInput { Url = "https://images.bazaar.test/x.jpg", Position = 0 } },
        Variants = new()
        {
            new VariantInput
            {
                Sku = sku,
                Title = "Default",
                Price = price,
                Currency = "USD",
                StockOnHand = 15,
                Options = new() { new VariantOptionInput { Name = "Size", Value = "One" } },
            },
        },
    };

    [Fact]
    public async Task Create_then_fetch_round_trips_a_product()
    {
        var create = await _client.PostAsJsonAsync("/api/admin/products", NewProduct("gadget-one", "GAD-1"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("gadget-one", created!.Slug);
        Assert.Single(created.Variants);
        Assert.Equal(15, created.Variants[0].Available);

        var fetched = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/admin/products/{created.Id}");
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("GAD-1", fetched.Variants[0].Sku);
    }

    [Fact]
    public async Task Create_with_missing_fields_returns_validation_problem()
    {
        var bad = new CreateProductRequest { Title = null, Slug = null, Variants = new() };
        var response = await _client.PostAsJsonAsync("/api/admin/products", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemPayload>();
        Assert.NotNull(problem);
        Assert.Contains(problem!.Errors.Keys, k => k.Contains("Title", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(problem.Errors.Keys, k => k.Contains("Variants", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Create_with_invalid_variant_price_returns_validation_problem()
    {
        var request = NewProduct("bad-price", "BADP-1");
        request = request with { Variants = new() { new VariantInput { Sku = "BADP-1", Price = null } } };

        var response = await _client.PostAsJsonAsync("/api/admin/products", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_duplicate_slug_returns_conflict()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/products", NewProduct("classic-tee", "NEW-SKU-1"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_status_and_details()
    {
        var create = await _client.PostAsJsonAsync("/api/admin/products", NewProduct("editable", "EDIT-1"));
        var created = await create.Content.ReadFromJsonAsync<ProductDetailDto>();

        var update = new UpdateProductRequest
        {
            Title = "Edited Title",
            Description = "Updated copy.",
            Status = "Draft",
            CollectionSlugs = new() { "apparel" },
        };
        var response = await _client.PutAsJsonAsync($"/api/admin/products/{created!.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ProductDetailDto>();
        Assert.Equal("Edited Title", updated!.Title);
        Assert.Equal("Draft", updated.Status);
        Assert.Contains("apparel", updated.Collections);
    }

    [Fact]
    public async Task Update_variant_price_and_stock()
    {
        var create = await _client.PostAsJsonAsync("/api/admin/products", NewProduct("varedit", "VE-1"));
        var created = await create.Content.ReadFromJsonAsync<ProductDetailDto>();
        var variantId = created!.Variants[0].Id;

        var response = await _client.PutAsJsonAsync($"/api/admin/variants/{variantId}",
            new UpdateVariantRequest { Price = 12.50m, StockOnHand = 3 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var variant = await response.Content.ReadFromJsonAsync<VariantDto>();
        Assert.Equal(12.50m, variant!.Price.Amount);
        Assert.Equal(3, variant.Available);
    }

    [Fact]
    public async Task Delete_removes_the_product()
    {
        var create = await _client.PostAsJsonAsync("/api/admin/products", NewProduct("deletable", "DEL-1"));
        var created = await create.Content.ReadFromJsonAsync<ProductDetailDto>();

        var delete = await _client.DeleteAsync($"/api/admin/products/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var fetch = await _client.GetAsync($"/api/admin/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, fetch.StatusCode);
    }

    [Fact]
    public async Task Collection_crud_round_trips()
    {
        var create = await _client.PostAsJsonAsync("/api/admin/collections",
            new UpsertCollectionRequest { Title = "Seasonal", Slug = "seasonal", Description = "Limited time." });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CollectionDto>();

        var update = await _client.PutAsJsonAsync($"/api/admin/collections/{created!.Id}",
            new UpsertCollectionRequest { Title = "Seasonal Picks", Slug = "seasonal" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<CollectionDto>();
        Assert.Equal("Seasonal Picks", updated!.Title);

        var delete = await _client.DeleteAsync($"/api/admin/collections/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    private sealed record ValidationProblemPayload(Dictionary<string, string[]> Errors);
}
