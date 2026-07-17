using Bazaar.Domain;
using Bazaar.Domain.Checkout;
using Bazaar.Domain.Common;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Payments;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Checkout;

public sealed record CheckoutCommand(string CartToken, string Email, Address ShippingAddress);

public enum CheckoutStatus
{
    Ok,
    CartNotFound,
    CartEmpty,
    InsufficientStock,
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
    private readonly IShippingCalculator _shipping;

    public CheckoutService(
        BazaarDbContext db,
        IPaymentGateway gateway,
        ITaxCalculator tax,
        IShippingCalculator shipping)
    {
        _db = db;
        _gateway = gateway;
        _tax = tax;
        _shipping = shipping;
    }

    public async Task<CheckoutOutcome> CheckoutAsync(CheckoutCommand command, CancellationToken ct = default)
    {
        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Variant!).ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Token == command.CartToken && c.Status == CartStatus.Open, ct);

        if (cart is null)
            return CheckoutOutcome.Fail(CheckoutStatus.CartNotFound, "Cart not found.");
        if (cart.Items.Count == 0)
            return CheckoutOutcome.Fail(CheckoutStatus.CartEmpty, "The cart is empty.");

        var variantIds = cart.Items.Select(i => i.VariantId).ToList();
        var inventory = await _db.InventoryItems
            .Where(i => variantIds.Contains(i.VariantId))
            .ToListAsync(ct);
        var inventoryByVariant = inventory.ToDictionary(i => i.VariantId);

        foreach (var item in cart.Items)
        {
            if (!inventoryByVariant.TryGetValue(item.VariantId, out var stock) || !stock.CanReserve(item.Quantity))
            {
                var sku = item.Variant?.Sku ?? item.VariantId.ToString();
                return CheckoutOutcome.Fail(CheckoutStatus.InsufficientStock, $"Not enough stock for '{sku}'.");
            }
        }

        // Reserve stock while we attempt payment (in-memory; only persisted if we save on success).
        foreach (var item in cart.Items)
            inventoryByVariant[item.VariantId].Reserve(item.Quantity);

        var currency = cart.Items[0].Variant!.Price.Currency;
        var subtotal = cart.Items.Aggregate(
            Money.Zero(currency),
            (acc, i) => acc.Add(i.Variant!.Price.MultiplyBy(i.Quantity)));
        var itemCount = cart.Items.Sum(i => i.Quantity);
        var tax = _tax.CalculateTax(subtotal);
        var shipping = _shipping.CalculateShipping(subtotal, itemCount);
        var discount = Money.Zero(currency);
        var grandTotal = subtotal.Add(tax).Add(shipping).Subtract(discount);

        var order = new Order
        {
            Number = await NextOrderNumberAsync(ct),
            Email = command.Email,
            Currency = currency,
            Status = OrderStatus.Pending,
            ShippingAddress = command.ShippingAddress,
            Subtotal = subtotal,
            DiscountTotal = discount,
            TaxTotal = tax,
            ShippingTotal = shipping,
            GrandTotal = grandTotal,
        };

        foreach (var item in cart.Items)
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

        foreach (var item in cart.Items)
            inventoryByVariant[item.VariantId].Commit(item.Quantity);

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
