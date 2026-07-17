using Bazaar.Domain.Payments;

namespace Bazaar.Infrastructure.Payments;

/// <summary>
/// A deterministic in-memory payment gateway for development and tests. It approves every charge
/// except when the customer email contains "decline" or the amount is non-positive, so tests can
/// exercise both the happy and failure paths without a real provider.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount.Amount <= 0m)
            return Task.FromResult(PaymentResult.Declined("Charge amount must be positive."));

        if (request.Email.Contains("decline", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(PaymentResult.Declined("The card was declined."));

        return Task.FromResult(PaymentResult.Success($"txn_{Guid.NewGuid():N}"));
    }
}
