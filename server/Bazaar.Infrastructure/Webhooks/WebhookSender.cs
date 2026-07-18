namespace Bazaar.Infrastructure.Webhooks;

public sealed record WebhookSendResult(bool Success, int? StatusCode);

/// <summary>Port that actually delivers a signed webhook payload to a subscriber URL.</summary>
public interface IWebhookSender
{
    Task<WebhookSendResult> SendAsync(string url, string payload, string signature, CancellationToken ct = default);
}

/// <summary>
/// Deterministic in-memory webhook sender for development and tests (mirrors the fake payment gateway):
/// URLs containing "fail" always error (so retry capping can be exercised); everything else returns 200.
/// A production adapter would POST the payload over HTTP with the signature header.
/// </summary>
public sealed class FakeWebhookSender : IWebhookSender
{
    public Task<WebhookSendResult> SendAsync(string url, string payload, string signature, CancellationToken ct = default)
    {
        if (url.Contains("fail", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new WebhookSendResult(false, 500));
        return Task.FromResult(new WebhookSendResult(true, 200));
    }
}
