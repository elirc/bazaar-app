using System.Security.Cryptography;
using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain.Webhooks;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AdminWebhookEndpoints
{
    public static IEndpointRouteBuilder MapAdminWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/webhooks").WithTags("Admin: Webhooks").RequireAuthorization("Admin");
        group.MapGet("/", ListSubscriptions);
        group.MapPost("/", CreateSubscription);
        group.MapDelete("/{id:guid}", DeleteSubscription);
        group.MapGet("/deliveries", ListDeliveries);
        return app;
    }

    private static async Task<IResult> ListSubscriptions(BazaarDbContext db, CancellationToken ct)
    {
        var subs = await db.WebhookSubscriptions.AsNoTracking().OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
        return Results.Ok(subs.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateSubscription(BazaarDbContext db, CreateWebhookRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var events = request.Events.Select(e => e.Trim()).Where(e => e.Length > 0).Distinct().ToList();
        var unknown = events.Where(e => !WebhookEvents.IsValid(e)).ToList();
        if (unknown.Count > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["events"] = new[] { $"Unknown event(s): {string.Join(", ", unknown)}. Valid events: {string.Join(", ", WebhookEvents.All)}." },
            });

        var subscription = new WebhookSubscription
        {
            Url = request.Url!.Trim(),
            Events = string.Join(",", events),
            Secret = string.IsNullOrWhiteSpace(request.Secret) ? GenerateSecret() : request.Secret!.Trim(),
            IsActive = true,
        };
        db.WebhookSubscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/webhooks/{subscription.Id}", ToDto(subscription));
    }

    private static async Task<IResult> DeleteSubscription(BazaarDbContext db, Guid id, CancellationToken ct)
    {
        var subscription = await db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscription is null) return Results.NotFound();
        db.WebhookSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListDeliveries(BazaarDbContext db, Guid? subscriptionId, CancellationToken ct)
    {
        var query = db.WebhookDeliveries.AsNoTracking().AsQueryable();
        if (subscriptionId is { } sid)
            query = query.Where(d => d.SubscriptionId == sid);

        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(100)
            .Select(d => new WebhookDeliveryDto(
                d.Id, d.SubscriptionId, d.Event, d.Url, d.Success, d.ResponseStatus, d.AttemptCount, d.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(deliveries);
    }

    private static WebhookSubscriptionDto ToDto(WebhookSubscription s) =>
        new(s.Id, s.Url, s.EventList.ToList(), s.Secret, s.IsActive, s.CreatedAt);

    private static string GenerateSecret() =>
        "whsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
}
