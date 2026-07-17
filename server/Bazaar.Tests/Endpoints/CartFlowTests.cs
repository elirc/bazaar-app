using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class CartFlowTests : IClassFixture<BazaarApiFactory>
{
    private readonly HttpClient _client;

    public CartFlowTests(BazaarApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<Guid> VariantId(string slug, string sku)
    {
        var product = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<CartDto> CreateCart()
    {
        var response = await _client.PostAsync("/api/cart", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartDto>())!;
    }

    [Fact]
    public async Task Cart_supports_add_update_and_remove()
    {
        var cart = await CreateCart();
        var teeId = await VariantId("classic-tee", "TEE-M-BLK");

        var afterAdd = await (await _client.PostAsJsonAsync($"/api/cart/{cart.Token}/items",
            new AddCartItemRequest { VariantId = teeId, Quantity = 2 })).Content.ReadFromJsonAsync<CartDto>();
        Assert.Equal(2, afterAdd!.ItemCount);
        Assert.Equal(39.98m, afterAdd.Subtotal.Amount); // 2 x 19.99
        Assert.Equal(2, afterAdd.Items[0].Quantity);

        var afterUpdate = await (await _client.PutAsJsonAsync($"/api/cart/{cart.Token}/items/{teeId}",
            new UpdateCartItemRequest { Quantity = 3 })).Content.ReadFromJsonAsync<CartDto>();
        Assert.Equal(3, afterUpdate!.ItemCount);
        Assert.Equal(59.97m, afterUpdate.Subtotal.Amount);

        var afterRemove = await (await _client.DeleteAsync($"/api/cart/{cart.Token}/items/{teeId}"))
            .Content.ReadFromJsonAsync<CartDto>();
        Assert.Empty(afterRemove!.Items);
        Assert.Equal(0, afterRemove.ItemCount);
    }

    [Fact]
    public async Task Adding_the_same_variant_merges_quantities()
    {
        var cart = await CreateCart();
        var mugId = await VariantId("ceramic-mug", "MUG-CRM");

        await _client.PostAsJsonAsync($"/api/cart/{cart.Token}/items", new AddCartItemRequest { VariantId = mugId, Quantity = 1 });
        var second = await (await _client.PostAsJsonAsync($"/api/cart/{cart.Token}/items",
            new AddCartItemRequest { VariantId = mugId, Quantity = 2 })).Content.ReadFromJsonAsync<CartDto>();

        Assert.Single(second!.Items);
        Assert.Equal(3, second.Items[0].Quantity);
    }

    [Fact]
    public async Task Exceeding_the_per_line_maximum_returns_400()
    {
        var cart = await CreateCart();
        var mugId = await VariantId("ceramic-mug", "MUG-CRM");

        var response = await _client.PostAsJsonAsync($"/api/cart/{cart.Token}/items",
            new AddCartItemRequest { VariantId = mugId, Quantity = 100 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adding_to_an_unknown_cart_returns_404()
    {
        var mugId = await VariantId("ceramic-mug", "MUG-CRM");
        var response = await _client.PostAsJsonAsync("/api/cart/not-a-real-token/items",
            new AddCartItemRequest { VariantId = mugId, Quantity = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_cart_returns_404_on_get()
    {
        var response = await _client.GetAsync("/api/cart/missing-token");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
