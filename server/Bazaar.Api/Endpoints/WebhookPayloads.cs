using Bazaar.Domain.Orders;

namespace Bazaar.Api.Endpoints;

/// <summary>Builds the compact JSON body sent to webhook subscribers for order events.</summary>
internal static class WebhookPayloads
{
    public static object ForOrder(Order order) => new
    {
        orderId = order.Id,
        number = order.Number,
        status = order.Status.ToString(),
        email = order.Email,
        grandTotal = order.GrandTotal.Amount,
        currency = order.Currency,
        placedAt = order.PlacedAt,
    };
}
