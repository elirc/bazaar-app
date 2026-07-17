using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Domain.Discounts;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminDiscountEndpoints
{
    public static IEndpointRouteBuilder MapAdminDiscountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/discounts").WithTags("Admin: Discounts").RequireAuthorization("Admin");
        group.MapGet("/", ListDiscounts);
        group.MapPost("/", CreateDiscount);
        group.MapDelete("/{id:guid}", DeleteDiscount);
        return app;
    }

    private static async Task<IResult> ListDiscounts(BazaarDbContext db, CancellationToken ct)
    {
        var discounts = await db.DiscountCodes.AsNoTracking()
            .OrderBy(d => d.Code)
            .ToListAsync(ct);
        return Results.Ok(discounts.Select(d => d.ToDto()).ToList());
    }

    private static async Task<IResult> CreateDiscount(BazaarDbContext db, CreateDiscountRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);
        if (!Enum.TryParse<DiscountType>(request.Type, true, out var type) || !Enum.IsDefined(type))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["type"] = new[] { "Type must be Percentage or FixedAmount." } });
        if (type == DiscountType.Percentage && request.Value > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["value"] = new[] { "A percentage discount cannot exceed 100." } });

        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.DiscountCodes.AnyAsync(d => d.Code == code, ct))
            return Results.Problem($"A discount code '{code}' already exists.", statusCode: StatusCodes.Status409Conflict, title: "Code already in use");

        var discount = new DiscountCode
        {
            Code = code,
            Type = type,
            Value = request.Value!.Value,
            Currency = request.Currency?.ToUpperInvariant() ?? Money.DefaultCurrency,
            IsActive = request.IsActive,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            UsageLimit = request.UsageLimit,
        };
        db.DiscountCodes.Add(discount);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/admin/discounts/{discount.Id}", discount.ToDto());
    }

    private static async Task<IResult> DeleteDiscount(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var discount = await db.DiscountCodes.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (discount is null) return Results.NotFound();
        db.DiscountCodes.Remove(discount);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
