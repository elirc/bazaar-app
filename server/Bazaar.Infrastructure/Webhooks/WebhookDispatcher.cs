using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bazaar.Domain.Webhooks;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Webhooks;

/// <summary>
/// Fans an order-lifecycle event out to every active subscription for it: builds a JSON payload,
/// HMAC-SHA256 signs it with the subscription secret, delivers it via the sender with capped retries,
/// and records each attempt in the delivery log. Best-effort — a failing endpoint never breaks the caller.
/// </summary>
public sealed class WebhookDispatcher
{
    public const int MaxAttempts = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly BazaarDbContext _db;
    private readonly IWebhookSender _sender;

    public WebhookDispatcher(BazaarDbContext db, IWebhookSender sender)
    {
        _db = db;
        _sender = sender;
    }

    public async Task DispatchAsync(string eventType, object data, CancellationToken ct = default)
    {
        var subscriptions = await _db.WebhookSubscriptions.AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(ct);
        var matching = subscriptions.Where(s => s.SubscribesTo(eventType)).ToList();
        if (matching.Count == 0) return;

        var payload = JsonSerializer.Serialize(
            new { @event = eventType, timestamp = DateTimeOffset.UtcNow, data }, JsonOptions);

        foreach (var subscription in matching)
        {
            var signature = Sign(payload, subscription.Secret);
            var attempts = 0;
            var success = false;
            int? status = null;

            while (attempts < MaxAttempts && !success)
            {
                attempts++;
                try
                {
                    var result = await _sender.SendAsync(subscription.Url, payload, signature, ct);
                    success = result.Success;
                    status = result.StatusCode;
                }
                catch
                {
                    success = false;
                    status = null;
                }
            }

            _db.WebhookDeliveries.Add(new WebhookDelivery
            {
                SubscriptionId = subscription.Id,
                Event = eventType,
                Url = subscription.Url,
                Payload = payload,
                Signature = signature,
                Success = success,
                ResponseStatus = status,
                AttemptCount = attempts,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
