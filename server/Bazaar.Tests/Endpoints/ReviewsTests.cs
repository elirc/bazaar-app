using System.Net;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests.Endpoints;

public class ReviewsTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public ReviewsTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> NewCustomer()
    {
        var client = _factory.CreateClient();
        var auth = await client.RegisterAsync($"rev-{Guid.NewGuid():N}@example.com", "supersecret");
        client.UseBearer(auth.Token);
        return client;
    }

    private async Task Purchase(HttpClient client, string slug, string sku)
    {
        var cart = await (await client.PostAsync("/api/cart", null)).Content.ReadFromJsonAsync<CartDto>();
        var product = await client.GetFromJsonAsync<ProductDetailDto>($"/api/storefront/products/{slug}");
        var variantId = product!.Variants.Single(v => v.Sku == sku).Id;
        await client.PostAsJsonAsync($"/api/cart/{cart!.Token}/items",
            new AddCartItemRequest { VariantId = variantId, Quantity = 1 });
        var checkout = new CheckoutRequest
        {
            CartToken = cart.Token,
            Email = "rev@example.com",
            ShippingAddress = new AddressInput { Name = "Rev", Line1 = "1 St", City = "Denver", PostalCode = "80202", Country = "US" },
        };
        (await client.PostAsJsonAsync("/api/checkout", checkout)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_non_purchaser_cannot_review_a_product()
    {
        var client = await NewCustomer();
        var response = await client.PostAsJsonAsync("/api/storefront/products/classic-tee/reviews",
            new CreateReviewRequest { Rating = 5, Body = "Never bought it." });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reviewing_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/storefront/products/classic-tee/reviews",
            new CreateReviewRequest { Rating = 5, Body = "Anon." });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rating_out_of_range_is_rejected()
    {
        var client = await NewCustomer();
        await Purchase(client, "merino-hoodie", "HOOD-M-GRY");
        var response = await client.PostAsJsonAsync("/api/storefront/products/merino-hoodie/reviews",
            new CreateReviewRequest { Rating = 9, Body = "Too high." });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_verified_purchase_review_is_pending_until_moderated_then_aggregates()
    {
        var customer = await NewCustomer();
        await Purchase(customer, "ceramic-mug", "MUG-CRM");

        var created = await (await customer.PostAsJsonAsync("/api/storefront/products/ceramic-mug/reviews",
            new CreateReviewRequest { Rating = 5, Title = "Lovely", Body = "Great mug." }))
            .Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(created);
        Assert.True(created!.IsVerifiedPurchase);

        // Pending: not visible on the storefront and not aggregated yet.
        var beforeList = await customer.GetFromJsonAsync<List<ReviewDto>>("/api/storefront/products/ceramic-mug/reviews");
        Assert.Empty(beforeList!);
        var beforeDetail = await customer.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/ceramic-mug");
        Assert.Equal(0, beforeDetail!.ReviewCount);

        // Admin approves it.
        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        var queue = await admin.GetFromJsonAsync<PagedResult<AdminReviewDto>>("/api/admin/reviews?status=Pending");
        Assert.Contains(queue!.Items, r => r.Id == created.Id);
        var moderate = await admin.PostAsJsonAsync($"/api/admin/reviews/{created.Id}/moderate",
            new ModerateReviewRequest { Status = "Approved" });
        Assert.Equal(HttpStatusCode.OK, moderate.StatusCode);

        // Now visible and aggregated.
        var afterList = await customer.GetFromJsonAsync<List<ReviewDto>>("/api/storefront/products/ceramic-mug/reviews");
        Assert.Single(afterList!);
        var afterDetail = await customer.GetFromJsonAsync<ProductDetailDto>("/api/storefront/products/ceramic-mug");
        Assert.Equal(1, afterDetail!.ReviewCount);
        Assert.Equal(5.0, afterDetail.AverageRating);
    }

    [Fact]
    public async Task A_customer_can_only_review_a_product_once()
    {
        var customer = await NewCustomer();
        await Purchase(customer, "canvas-tote", "TOTE-NAT");

        var first = await customer.PostAsJsonAsync("/api/storefront/products/canvas-tote/reviews",
            new CreateReviewRequest { Rating = 4, Body = "Good tote." });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await customer.PostAsJsonAsync("/api/storefront/products/canvas-tote/reviews",
            new CreateReviewRequest { Rating = 3, Body = "Changed my mind." });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Helpful_votes_increment_once_per_customer()
    {
        var author = await NewCustomer();
        await Purchase(author, "leather-belt", "BELT-32");
        var review = await (await author.PostAsJsonAsync("/api/storefront/products/leather-belt/reviews",
            new CreateReviewRequest { Rating = 5, Body = "Solid belt." }))
            .Content.ReadFromJsonAsync<ReviewDto>();

        var admin = _factory.CreateClient();
        await admin.AuthenticateAdminAsync();
        await admin.PostAsJsonAsync($"/api/admin/reviews/{review!.Id}/moderate",
            new ModerateReviewRequest { Status = "Approved" });

        var voter = await NewCustomer();
        var first = await voter.PostAsync($"/api/storefront/reviews/{review.Id}/helpful", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await voter.PostAsync($"/api/storefront/reviews/{review.Id}/helpful", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Moderation_queue_requires_admin()
    {
        var customer = await NewCustomer();
        var response = await customer.GetAsync("/api/admin/reviews");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
