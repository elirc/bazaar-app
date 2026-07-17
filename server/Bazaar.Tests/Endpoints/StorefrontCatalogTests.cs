using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class StorefrontCatalogTests : IClassFixture<BazaarApiFactory>
{
    private readonly HttpClient _client;

    public StorefrontCatalogTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Products_lists_active_catalog_with_paging_metadata()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/storefront/products");

        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(6, page.TotalCount);
        Assert.Equal(6, page.Items.Count);
        Assert.All(page.Items, p => Assert.Equal("Active", p.Status));
    }

    [Fact]
    public async Task Products_paginate()
    {
        var page1 = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/storefront/products?page=1&pageSize=2");
        var page2 = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/storefront/products?page=2&pageSize=2");

        Assert.Equal(2, page1!.Items.Count);
        Assert.Equal(3, page1.TotalPages);
        Assert.True(page1.HasNext);
        Assert.False(page1.HasPrevious);
        Assert.True(page2!.HasPrevious);
        Assert.NotEqual(page1.Items[0].Slug, page2.Items[0].Slug);
    }

    [Fact]
    public async Task Products_search_matches_title()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/storefront/products?search=hoodie");

        Assert.Single(page!.Items);
        Assert.Equal("merino-hoodie", page.Items[0].Slug);
    }

    [Fact]
    public async Task Products_filter_by_collection()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/storefront/products?collection=apparel");

        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, p => Assert.Contains("apparel", p.Collections));
    }

    [Fact]
    public async Task Products_sort_by_price_ascending()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<ProductSummaryDto>>("/api/storefront/products?sort=price_asc&pageSize=60");

        var prices = page!.Items.Select(p => p.PriceFrom!.Amount).ToList();
        var sorted = prices.OrderBy(a => a).ToList();
        Assert.Equal(sorted, prices);
        Assert.Equal(14.00m, prices.First()); // cheapest is the stoneware mug
    }

    [Fact]
    public async Task Product_detail_returns_variants_with_availability()
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/classic-tee");

        Assert.NotNull(product);
        Assert.Equal("Classic Cotton Tee", product!.Title);
        Assert.Equal(3, product.Variants.Count);
        var small = product.Variants.Single(v => v.Sku == "TEE-S-BLK");
        Assert.Equal(40, small.Available);
        Assert.Contains(small.Options, o => o is { Name: "Size", Value: "Small" });
    }

    [Fact]
    public async Task Unknown_product_slug_returns_404()
    {
        var response = await _client.GetAsync("/api/storefront/products/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Collections_list_with_active_product_counts()
    {
        var collections = await _client.GetFromJsonAsync<List<CollectionDto>>("/api/storefront/collections");

        Assert.Equal(3, collections!.Count);
        var apparel = collections.Single(c => c.Slug == "apparel");
        Assert.Equal(2, apparel.ProductCount);
    }
}
