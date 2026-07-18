using Bazaar.Domain.Webhooks;
using Bazaar.Infrastructure.Webhooks;

namespace Bazaar.Tests.Domain;

public class WebhookSignatureTests
{
    [Fact]
    public void Signature_is_a_deterministic_hmac_sha256_hex()
    {
        const string payload = "{\"event\":\"order.paid\"}";
        var a = WebhookDispatcher.Sign(payload, "whsec_secret");
        var b = WebhookDispatcher.Sign(payload, "whsec_secret");
        var different = WebhookDispatcher.Sign(payload, "other-secret");

        Assert.Equal(a, b);                 // deterministic
        Assert.NotEqual(a, different);      // depends on the secret
        Assert.Equal(64, a.Length);         // 32-byte SHA-256 as hex
        Assert.Matches("^[0-9a-f]+$", a);
    }

    [Fact]
    public void Subscription_matches_only_its_subscribed_events()
    {
        var sub = new WebhookSubscription { Events = "order.paid, order.refunded" };
        Assert.True(sub.SubscribesTo("order.paid"));
        Assert.True(sub.SubscribesTo("order.refunded"));
        Assert.False(sub.SubscribesTo("order.created"));
    }
}
