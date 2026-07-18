namespace Bazaar.Domain.Webhooks;

/// <summary>The webhook event names Bazaar can emit from the order lifecycle.</summary>
public static class WebhookEvents
{
    public const string OrderCreated = "order.created";
    public const string OrderPaid = "order.paid";
    public const string OrderFulfilled = "order.fulfilled";
    public const string OrderRefunded = "order.refunded";

    public static readonly string[] All = { OrderCreated, OrderPaid, OrderFulfilled, OrderRefunded };

    public static bool IsValid(string ev) => Array.IndexOf(All, ev) >= 0;
}

/// <summary>A subscriber endpoint that receives HMAC-signed webhook deliveries for selected events.</summary>
public class WebhookSubscription
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;

    /// <summary>Shared secret used to HMAC-sign each delivery payload.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Comma-separated event names this endpoint is subscribed to.</summary>
    public string Events { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IEnumerable<string> EventList =>
        Events.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool SubscribesTo(string ev) =>
        EventList.Any(e => string.Equals(e, ev, StringComparison.OrdinalIgnoreCase));
}
