using Bazaar.Domain.Common;

namespace Bazaar.Domain.Payments;

/// <summary>Port for charging a customer. Adapters (real or fake) live in the infrastructure layer.</summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);
}

public sealed record PaymentRequest(string Reference, Money Amount, string Email);

public sealed record PaymentResult(bool Succeeded, string? TransactionId, string? FailureReason)
{
    public static PaymentResult Success(string transactionId) => new(true, transactionId, null);
    public static PaymentResult Declined(string reason) => new(false, null, reason);
}
