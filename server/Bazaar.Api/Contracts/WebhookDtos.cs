using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record WebhookSubscriptionDto(
    Guid Id,
    string Url,
    IReadOnlyList<string> Events,
    string Secret,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record WebhookDeliveryDto(
    Guid Id,
    Guid SubscriptionId,
    string Event,
    string Url,
    bool Success,
    int? ResponseStatus,
    int AttemptCount,
    DateTimeOffset CreatedAt);

public sealed record CreateWebhookRequest
{
    [Required, Url, StringLength(1000)]
    public string? Url { get; init; }

    [MinLength(1, ErrorMessage = "Subscribe to at least one event.")]
    public List<string> Events { get; init; } = new();

    [StringLength(200, MinimumLength = 8)]
    public string? Secret { get; init; }
}
