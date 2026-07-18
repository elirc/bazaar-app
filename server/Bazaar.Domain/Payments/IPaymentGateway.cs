using Bazaar.Domain.Common;

namespace Bazaar.Domain.Payments;

/// <summary>Port for charging and refunding a customer. Adapters (real or fake) live in the infrastructure layer.</summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);

    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default);
}

public sealed record PaymentRequest(string Reference, Money Amount, string Email);

public sealed record PaymentResult(bool Succeeded, string? TransactionId, string? FailureReason)
{
    public static PaymentResult Success(string transactionId) => new(true, transactionId, null);
    public static PaymentResult Declined(string reason) => new(false, null, reason);
}

public sealed record RefundRequest(string Reference, Money Amount, string Email);

public sealed record RefundResult(bool Succeeded, string? RefundId, string? FailureReason)
{
    public static RefundResult Success(string refundId) => new(true, refundId, null);
    public static RefundResult Failed(string reason) => new(false, null, reason);
}
