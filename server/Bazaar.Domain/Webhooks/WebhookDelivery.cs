namespace Bazaar.Domain.Webhooks;

/// <summary>An audit record of one attempt-capped webhook delivery, kept as a delivery log.</summary>
public class WebhookDelivery
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? ResponseStatus { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
