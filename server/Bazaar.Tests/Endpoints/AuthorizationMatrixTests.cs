using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bazaar.Api.Contracts;
using Bazaar.Infrastructure.Auth;
using Bazaar.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Bazaar.Tests.Endpoints;

/// <summary>
/// Authorization surface: the guest/customer/admin matrix across admin endpoints, cross-account
/// isolation (a customer cannot touch another's wishlist), and role enforcement — the standard
/// ClaimTypes.Role claim the app issues grants admin, while a token with the wrong role value or no
/// role at all is authenticated yet forbidden.
/// </summary>
public class AuthorizationMatrixTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public AuthorizationMatrixTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> Customer()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"authz-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);
        return client;
    }

    /// <summary>Forge a validly-signed JWT with an arbitrary claim set (to probe authorization, not authentication).</summary>
    private string ForgeToken(IReadOnlyDictionary<string, object> extraClaims)
    {
        var options = _factory.Services.GetRequiredService<AuthOptions>();
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["email"] = "forged@example.com",
            ["name"] = "Forged User",
            ["iss"] = options.Issuer,
            ["aud"] = options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(30).ToUnixTimeSeconds(),
        };
        foreach (var (key, value) in extraClaims)
            payload[key] = value;

        static string Enc(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var signingInput = $"{Enc(JsonSerializer.SerializeToUtf8Bytes(header))}.{Enc(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.SigningKey));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        return $"{signingInput}.{Enc(signature)}";
    }

    [Theory]
    [InlineData("/api/admin/orders")]
    [InlineData("/api/admin/returns")]
    [InlineData("/api/admin/gift-cards")]
    [InlineData("/api/admin/webhooks")]
    [InlineData("/api/admin/reports/sales")]
    public async Task Guests_are_unauthorized_customers_are_forbidden_and_admins_are_allowed(string endpoint)
    {
        var guest = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(endpoint)).StatusCode);

        var customer = await Customer();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(endpoint)).StatusCode);

        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(endpoint)).StatusCode);
    }

    [Fact]
    public async Task The_standard_role_claim_grants_admin_access()
    {
        // The app issues its admin role under the standard ClaimTypes.Role claim; RequireRole("Admin")
        // admits it. This is the claim the real login mints (see JwtTokenService).
        var client = _factory.CreateClient();
        client.UseBearer(ForgeToken(new Dictionary<string, object> { [ClaimTypes.Role] = "Admin" }));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/products")).StatusCode);
    }

    [Fact]
    public async Task A_non_admin_role_claim_is_authenticated_but_forbidden()
    {
        // A well-formed token whose role is Customer is authenticated, yet the Admin policy rejects it.
        var client = _factory.CreateClient();
        client.UseBearer(ForgeToken(new Dictionary<string, object> { [ClaimTypes.Role] = "Customer" }));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/products")).StatusCode);
    }

    [Fact]
    public async Task A_token_with_no_role_claim_is_authenticated_but_forbidden()
    {
        // Authentication alone is not authorization: without a role claim the Admin policy denies access.
        var client = _factory.CreateClient();
        client.UseBearer(ForgeToken(new Dictionary<string, object>()));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/products")).StatusCode);
    }

    [Fact]
    public async Task A_customer_cannot_touch_another_customers_wishlist()
    {
        var alice = await Customer();
        // Listing creates Alice's default wishlist.
        var aliceLists = await alice.GetFromJsonAsync<List<WishlistDto>>("/api/account/wishlists");
        var aliceWishlistId = aliceLists!.Single().Id;

        var product = await alice.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/ceramic-mug");
        var variantId = product!.Variants[0].Id;

        var bob = await Customer();
        var add = await bob.PostAsJsonAsync($"/api/account/wishlists/{aliceWishlistId}/items",
            new AddWishlistItemRequest { VariantId = variantId });
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode); // Bob sees no such wishlist (no existence leak)

        var delete = await bob.DeleteAsync($"/api/account/wishlists/{aliceWishlistId}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
