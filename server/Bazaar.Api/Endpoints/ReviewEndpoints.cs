using System.Security.Claims;
using Bazaar.Api.Auth;
using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Reviews;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/storefront").WithTags("Reviews");
        group.MapGet("/products/{slug}/reviews", ListReviews);
        group.MapPost("/products/{slug}/reviews", CreateReview).RequireAuthorization();
        group.MapPost("/reviews/{id:guid}/helpful", MarkHelpful).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> ListReviews(BazaarDbContext db, string slug, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == ProductStatus.Active, ct);
        if (product is null) return Results.NotFound();

        var reviews = await db.ProductReviews.AsNoTracking()
            .Where(r => r.ProductId == product.Id && r.Status == ReviewStatus.Approved)
            .OrderByDescending(r => r.HelpfulCount).ThenByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto(
                r.Id, r.AuthorName, r.Rating, r.Title, r.Body, r.IsVerifiedPurchase, r.HelpfulCount, r.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(reviews);
    }

    private static async Task<IResult> CreateReview(
        BazaarDbContext db, ClaimsPrincipal principal, string slug, CreateReviewRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var product = await db.Products.AsNoTracking()
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == ProductStatus.Active, ct);
        if (product is null) return Results.NotFound();

        // Verified-purchase gate: the customer must have a paid/fulfilled order containing this product.
        var variantIds = product.Variants.Select(v => v.Id).ToList();
        var purchased = await db.Orders
            .Where(o => o.CustomerId == customerId && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Fulfilled))
            .SelectMany(o => o.Items)
            .AnyAsync(li => li.VariantId != null && variantIds.Contains(li.VariantId.Value), ct);
        if (!purchased)
            return Results.Problem(
                "Only customers who have purchased this product can review it.",
                statusCode: StatusCodes.Status403Forbidden, title: "Purchase required");

        if (await db.ProductReviews.AnyAsync(r => r.ProductId == product.Id && r.CustomerId == customerId, ct))
            return Results.Problem(
                "You have already reviewed this product.",
                statusCode: StatusCodes.Status409Conflict, title: "Already reviewed");

        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Id == customerId, ct);

        var review = new ProductReview
        {
            ProductId = product.Id,
            CustomerId = customerId.Value,
            AuthorName = customer.DisplayName,
            Rating = request.Rating,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title!.Trim(),
            Body = request.Body!.Trim(),
            Status = ReviewStatus.Pending,
            IsVerifiedPurchase = true,
        };
        db.ProductReviews.Add(review);
        await db.SaveChangesAsync(ct);

        var dto = new ReviewDto(
            review.Id, review.AuthorName, review.Rating, review.Title, review.Body,
            review.IsVerifiedPurchase, review.HelpfulCount, review.CreatedAt);
        return Results.Created($"/api/storefront/products/{slug}/reviews", dto);
    }

    private static async Task<IResult> MarkHelpful(
        BazaarDbContext db, ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var customerId = principal.GetCustomerId();
        if (customerId is null) return Results.Unauthorized();

        var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == id && r.Status == ReviewStatus.Approved, ct);
        if (review is null) return Results.NotFound();

        if (await db.ReviewVotes.AnyAsync(v => v.ReviewId == id && v.CustomerId == customerId, ct))
            return Results.Problem(
                "You have already marked this review as helpful.",
                statusCode: StatusCodes.Status409Conflict, title: "Already voted");

        db.ReviewVotes.Add(new ReviewVote { ReviewId = id, CustomerId = customerId.Value });
        review.HelpfulCount += 1;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { helpfulCount = review.HelpfulCount });
    }
}
