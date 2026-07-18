using Bazaar.Domain;
using Bazaar.Domain.Checkout;
using Bazaar.Domain.Common;
using Bazaar.Domain.Discounts;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Payments;
using Bazaar.Domain.Shipping;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Checkout;

public sealed record CheckoutCommand(
    string CartToken,
    string Email,
    Address ShippingAddress,
    string? DiscountCode = null,
    Guid? CustomerId = null,
    string? ShippingMethodCode = null);

public enum CheckoutStatus
{
    Ok,
    CartNotFound,
    CartEmpty,
    InsufficientStock,
    InvalidDiscount,
    InvalidShippingMethod,
    PaymentDeclined,
}

public sealed record CheckoutOutcome(CheckoutStatus Status, Order? Order, string? Detail)
{
    public static CheckoutOutcome Success(Order order) => new(CheckoutStatus.Ok, order, null);
    public static CheckoutOutcome Fail(CheckoutStatus status, string detail) => new(status, null, detail);
}

/// <summary>
/// Turns an open cart into a paid order: validates and reserves stock, computes tax + shipping,
/// charges the payment gateway, then commits inventory and marks the order paid on success.
/// </summary>
public sealed class CheckoutService
{
    private readonly BazaarDbContext _db;
    private readonly IPaymentGateway _gateway;
    private readonly ITaxCalculator _tax;

    public CheckoutService(
        BazaarDbContext db,
        IPaymentGateway gateway,
        ITaxCalculator tax)
    {
        _db = db;
        _gateway = gateway;
        _tax = tax;
    }

    public async Task<CheckoutOutcome> CheckoutAsync(CheckoutCommand command, CancellationToken ct = default)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Token == command.CartToken && c.Status == CartStatus.Open, ct);

        if (cart is null)
            return CheckoutOutcome.Fail(CheckoutStatus.CartNotFound, "Cart not found.");

        // Saved-for-later lines never enter checkout.
        var activeItems = cart.Items.Where(i => !i.SavedForLater).ToList();
        if (activeItems.Count == 0)
            return CheckoutOutcome.Fail(CheckoutStatus.CartEmpty, "The cart is empty.");

        var variantIds = activeItems.Select(i => i.VariantId).ToList();
        var inventory = await _db.InventoryItems
            .Where(i => variantIds.Contains(i.VariantId))
            .ToListAsync(ct);
        var inventoryByVariant = inventory.ToDictionary(i => i.VariantId);

        foreach (var item in activeItems)
        {
            if (!inventoryByVariant.TryGetValue(item.VariantId, out var stock) || !stock.CanReserve(item.Quantity))
            {
                var sku = item.Variant?.Sku ?? item.VariantId.ToString();
                return CheckoutOutcome.Fail(CheckoutStatus.InsufficientStock, $"Not enough stock for '{sku}'.");
            }
        }

        // Reserve stock while we attempt payment (in-memory; only persisted if we save on success).
        foreach (var item in activeItems)
            inventoryByVariant[item.VariantId].Reserve(item.Quantity);

        var currency = activeItems[0].Variant!.Price.Currency;
        var subtotal = activeItems.Aggregate(
            Money.Zero(currency),
            (acc, i) => acc.Add(i.Variant!.Price.MultiplyBy(i.Quantity)));
        var itemCount = activeItems.Sum(i => i.Quantity);
        var tax = _tax.CalculateTax(subtotal);

        // Resolve the shipping method (requested code, else the default) and price it.
        var methods = await _db.ShippingMethods.Where(m => m.IsActive).ToListAsync(ct);
        ShippingMethod? method;
        if (!string.IsNullOrWhiteSpace(command.ShippingMethodCode))
        {
            var code = command.ShippingMethodCode.Trim().ToLowerInvariant();
            method = methods.FirstOrDefault(m => m.Code == code);
            if (method is null)
                return CheckoutOutcome.Fail(CheckoutStatus.InvalidShippingMethod, "That shipping method is not available.");
        }
        else
        {
            method = methods.FirstOrDefault(m => m.IsDefault) ?? methods.OrderBy(m => m.DisplayOrder).FirstOrDefault();
        }

        var totalWeightGrams = activeItems.Sum(i => i.Variant!.WeightGrams * i.Quantity);
        var shipping = method?.CalculateCost(subtotal, itemCount, totalWeightGrams) ?? Money.Zero(currency);

        var discount = Money.Zero(currency);
        DiscountCode? appliedCode = null;
        if (!string.IsNullOrWhiteSpace(command.DiscountCode))
        {
            var normalized = command.DiscountCode.Trim().ToUpperInvariant();
            appliedCode = await _db.DiscountCodes.FirstOrDefaultAsync(d => d.Code == normalized, ct);
            if (appliedCode is null || !appliedCode.IsRedeemable(DateTimeOffset.UtcNow))
                return CheckoutOutcome.Fail(CheckoutStatus.InvalidDiscount, "That discount code is not valid.");
            discount = appliedCode.ComputeDiscount(subtotal);
        }

        var grandTotal = subtotal.Add(tax).Add(shipping).Subtract(discount);

        var order = new Order
        {
            Number = await NextOrderNumberAsync(ct),
            CustomerId = command.CustomerId ?? cart.CustomerId,
            Email = command.Email,
            Currency = currency,
            Status = OrderStatus.Pending,
            ShippingAddress = command.ShippingAddress,
            Subtotal = subtotal,
            DiscountTotal = discount,
            TaxTotal = tax,
            ShippingTotal = shipping,
            GrandTotal = grandTotal,
            DiscountCode = appliedCode?.Code,
            ShippingMethod = method?.Name,
        };

        foreach (var item in activeItems)
        {
            var variant = item.Variant!;
            var productTitle = variant.Product?.Title ?? variant.Title;
            order.AddItem(new OrderLineItem
            {
                VariantId = variant.Id,
                Sku = variant.Sku,
                Title = variant.Title == "Default" ? productTitle : $"{productTitle} ({variant.Title})",
                Quantity = item.Quantity,
                UnitPrice = variant.Price,
                LineTotal = variant.Price.MultiplyBy(item.Quantity),
            });
        }

        var payment = await _gateway.ChargeAsync(new PaymentRequest(order.Number, grandTotal, command.Email), ct);
        if (!payment.Succeeded)
            return CheckoutOutcome.Fail(CheckoutStatus.PaymentDeclined, payment.FailureReason ?? "Payment was declined.");

        foreach (var item in activeItems)
            inventoryByVariant[item.VariantId].Commit(item.Quantity);

        appliedCode?.MarkRedeemed();
        order.TransitionTo(OrderStatus.Paid);
        cart.Status = CartStatus.Converted;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        return CheckoutOutcome.Success(order);
    }

    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var count = await _db.Orders.CountAsync(ct);
        return $"BZ-{1001 + count}";
    }
}
