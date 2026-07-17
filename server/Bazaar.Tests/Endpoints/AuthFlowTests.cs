using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Infrastructure.Auth;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class AuthFlowTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public AuthFlowTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private static CheckoutRequest Checkout(string token) => new()
    {
        CartToken = token,
        Email = "buyer@example.com",
        ShippingAddress = new AddressInput
        {
            Name = "Ada Lovelace", Line1 = "1 Analytical Way", City = "London",
            PostalCode = "EC1A", Country = "GB",
        },
    };

    private async Task<Guid> VariantId(HttpClient client, string slug, string sku)
    {
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        return product!.Variants.Single(v => v.Sku == sku).Id;
    }

    private async Task<OrderDto> PlaceOrder(HttpClient client, string slug, string sku, int qty)
    {
        var cart = await (await client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var variantId = await VariantId(client, slug, sku);
        await client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = variantId, Quantity = qty });
        var response = await client.PostAsJsonAsync("/api/checkout", Checkout(cart.Token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!;
    }

    [Fact]
    public async Task Register_returns_a_token_and_a_customer_profile()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "newbie@example.com", Password = "supersecret", FirstName = "New" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal("newbie@example.com", auth.Customer.Email);
        Assert.Equal("Customer", auth.Customer.Role);
    }

    [Fact]
    public async Task Register_with_a_short_password_is_rejected()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "shortpw@example.com", Password = "tiny" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_a_duplicate_email_returns_409()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("dupe@example.com", "supersecret");
        var again = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "dupe@example.com", Password = "supersecret" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Login_with_a_bad_password_returns_401()
    {
        var client = _factory.CreateClient();
        await client.RegisterAsync("loginer@example.com", "supersecret");
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "loginer@example.com", Password = "wrongpassword" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var client = _factory.CreateClient();
        var anon = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var auth = await client.RegisterAsync("meuser@example.com", "supersecret");
        client.UseBearer(auth.Token);
        var me = await client.GetFromJsonAsync<CustomerDto>("/api/auth/me");
        Assert.Equal("meuser@example.com", me!.Email);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]   // guest
    [InlineData("customer", HttpStatusCode.Forbidden)] // signed-in non-admin
    [InlineData("admin", HttpStatusCode.OK)]           // admin
    public async Task Admin_endpoints_enforce_the_role_matrix(string? who, HttpStatusCode expected)
    {
        var client = _factory.CreateClient();
        if (who == "customer")
        {
            var auth = await client.RegisterAsync($"matrix-{Guid.NewGuid():N}@example.com", "supersecret");
            client.UseBearer(auth.Token);
        }
        else if (who == "admin")
        {
            await client.AuthenticateAdminAsync();
        }

        var response = await client.GetAsync("/api/admin/products");
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_checkout_attaches_the_order_to_the_account_history()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"history-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);

        var order = await PlaceOrder(client, "ceramic-mug", "MUG-CRM", 1);

        var history = await client.GetFromJsonAsync<List<OrderSummaryDto>>("/api/account/orders");
        Assert.Contains(history!, o => o.Number == order.Number);

        var detail = await client.GetFromJsonAsync<OrderDto>($"/api/account/orders/{order.Id}");
        Assert.Equal(order.Number, detail!.Number);
    }

    [Fact]
    public async Task A_customer_cannot_read_another_customers_order()
    {
        var alice = _factory.CreateClient();
        var aliceAuth = await alice.RegisterAsync($"alice-{Guid.NewGuid():N}@example.com", "supersecret");
        alice.UseBearer(aliceAuth.Token);
        var aliceOrder = await PlaceOrder(alice, "canvas-tote", "TOTE-OLV", 1);

        var bob = _factory.CreateClient();
        var bobAuth = await bob.RegisterAsync($"bob-{Guid.NewGuid():N}@example.com", "supersecret");
        bob.UseBearer(bobAuth.Token);

        var cross = await bob.GetAsync($"/api/account/orders/{aliceOrder.Id}");
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        var bobHistory = await bob.GetFromJsonAsync<List<OrderSummaryDto>>("/api/account/orders");
        Assert.DoesNotContain(bobHistory!, o => o.Number == aliceOrder.Number);
    }

    [Fact]
    public async Task Guest_checkout_still_works_without_an_account()
    {
        var client = _factory.CreateClient(); // no bearer token
        var order = await PlaceOrder(client, "classic-tee", "TEE-M-BLK", 1);
        Assert.Equal("Paid", order.Status);
    }

    [Fact]
    public async Task Account_orders_require_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/account/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
