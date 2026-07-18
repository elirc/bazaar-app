using System.Security.Cryptography;
using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain.Common;
using Bazaar.Domain.GiftCards;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class GiftCardEndpoints
{
    public static IEndpointRouteBuilder MapGiftCardEndpoints(this IEndpointRouteBuilder app)
    {
        // Public balance lookup.
        app.MapGet("/api/storefront/gift-cards/{code}", CheckBalance).WithTags("Gift cards");

        // Admin issuance + listing.
        var admin = app.MapGroup("/api/admin/gift-cards").WithTags("Admin: Gift cards").RequireAuthorization("Admin");
        admin.MapGet("/", ListGiftCards);
        admin.MapPost("/", IssueGiftCard);
        return app;
    }

    private static async Task<IResult> CheckBalance(BazaarDbContext db, string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var card = await db.GiftCards.AsNoTracking().FirstOrDefaultAsync(g => g.Code == normalized, ct);
        if (card is null || !card.IsRedeemable)
            return Results.Ok(new GiftCardBalanceDto(normalized, false, null));
        return Results.Ok(new GiftCardBalanceDto(card.Code, true, card.Balance.ToDto()));
    }

    private static async Task<IResult> ListGiftCards(BazaarDbContext db, CancellationToken ct)
    {
        var cards = await db.GiftCards.AsNoTracking().OrderByDescending(g => g.CreatedAt).ToListAsync(ct);
        return Results.Ok(cards.Select(ToDto).ToList());
    }

    private static async Task<IResult> IssueGiftCard(BazaarDbContext db, IssueGiftCardRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? Money.DefaultCurrency : request.Currency!.ToUpperInvariant();
        var code = string.IsNullOrWhiteSpace(request.Code) ? GenerateCode() : request.Code!.Trim().ToUpperInvariant();

        if (await db.GiftCards.AnyAsync(g => g.Code == code, ct))
            return Results.Problem($"A gift card '{code}' already exists.", statusCode: StatusCodes.Status409Conflict, title: "Code already in use");

        // Distinct Money instances: EF tracks the two owned navigations separately.
        var card = new GiftCard
        {
            Code = code,
            InitialBalance = new Money(request.Amount!.Value, currency),
            Balance = new Money(request.Amount!.Value, currency),
            IsActive = true,
        };
        db.GiftCards.Add(card);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/gift-cards/{card.Id}", ToDto(card));
    }

    private static GiftCardDto ToDto(GiftCard g) =>
        new(g.Id, g.Code, g.Balance.ToDto(), g.InitialBalance.ToDto(), g.IsActive, g.CreatedAt);

    private static string GenerateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
        Span<char> chars = stackalloc char[12];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return "GC-" + new string(chars);
    }
}
