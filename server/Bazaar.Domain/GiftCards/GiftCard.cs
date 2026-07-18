using Bazaar.Domain.Common;

namespace Bazaar.Domain.GiftCards;

/// <summary>A prepaid gift card that can be redeemed as partial or full tender at checkout.</summary>
public class GiftCard
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public Money InitialBalance { get; set; } = Money.Zero();
    public Money Balance { get; set; } = Money.Zero();
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsRedeemable => IsActive && Balance.Amount > 0m;

    /// <summary>The amount this card can cover toward a total: the lesser of its balance and the total.</summary>
    public Money AmountToApply(Money total)
    {
        if (!IsRedeemable) return Money.Zero(total.Currency);
        // Always a fresh Money instance so callers can assign it to a separate owned navigation.
        var applied = Balance.Amount >= total.Amount ? total.Amount : Balance.Amount;
        return new Money(applied, total.Currency);
    }

    /// <summary>Deduct a redeemed amount from the balance (never below zero).</summary>
    public void Redeem(Money amount)
    {
        var next = Balance.Amount - amount.Amount;
        Balance = new Money(next < 0m ? 0m : next, Balance.Currency);
    }

    /// <summary>
    /// Restore refunded value to the balance (e.g. when a gift-card-funded order is returned).
    /// A fresh Money instance keeps the owned navigation independent for EF.
    /// </summary>
    public void Restore(Money amount)
    {
        if (amount.IsNegative) return;
        Balance = new Money(Balance.Amount + amount.Amount, Balance.Currency);
    }
}
