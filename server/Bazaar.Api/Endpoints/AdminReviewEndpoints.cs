using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminReviewEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/reviews").WithTags("Admin: Reviews").RequireAuthorization("Admin");
        group.MapGet("/", ListReviews);
        group.MapPost("/{id:guid}/moderate", Moderate);
        return app;
    }

    private static async Task<IResult> ListReviews(
        BazaarDbContext db, string? status, int? page, int? pageSize, CancellationToken ct)
    {
        var (pageNumber, size) = Paging.Clamp(page, pageSize, DefaultPageSize, MaxPageSize);

        var query = from r in db.ProductReviews.AsNoTracking()
                    join p in db.Products.AsNoTracking() on r.ProductId equals p.Id
                    select new { r, p.Title, p.Slug };

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReviewStatus>(status, true, out var parsed))
            query = query.Where(x => x.r.Status == parsed);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.r.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(x => new AdminReviewDto(
                x.r.Id, x.r.ProductId, x.Title, x.Slug, x.r.AuthorName, x.r.Rating, x.r.Title!,
                x.r.Body, x.r.Status.ToString(), x.r.IsVerifiedPurchase, x.r.HelpfulCount, x.r.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<AdminReviewDto>(items, pageNumber, size, total));
    }

    private static async Task<IResult> Moderate(
        BazaarDbContext db, Guid id, ModerateReviewRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);
        if (!Enum.TryParse<ReviewStatus>(request.Status, true, out var target) || !Enum.IsDefined(target))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = new[] { "Unknown review status." } });

        var review = await db.ProductReviews.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (review is null) return Results.NotFound();

        review.Moderate(target);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { review.Id, Status = review.Status.ToString() });
    }
}
