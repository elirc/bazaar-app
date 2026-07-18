using Bazaar.Domain;
using Bazaar.Domain.Carts;
using Bazaar.Domain.Catalog;
using Bazaar.Domain.Common;
using Bazaar.Domain.Discounts;
using Bazaar.Domain.Inventory;
using Bazaar.Domain.Orders;

namespace Bazaar.Tests.Domain;

public class DomainBehaviorTests
{
    private static ProductVariant Variant(decimal price = 10m) =>
        new() { Sku = "SKU-1", Price = new Money(price, "USD") };

    [Fact]
    public void Cart_merges_quantities_for_the_same_variant()
    {
        var cart = new Cart();
        var variant = Variant();

        cart.AddItem(variant, 2);
        cart.AddItem(variant, 3);

        Assert.Single(cart.Items);
        Assert.Equal(5, cart.TotalQuantity);
    }

    [Fact]
    public void Cart_enforces_the_per_line_maximum()
    {
        var cart = new Cart();
        var variant = Variant();

        Assert.Throws<InvalidOperationException>(() => cart.AddItem(variant, Cart.MaxQuantityPerLine + 1));
    }

    [Fact]
    public void Cart_update_to_zero_removes_the_line()
    {
        var cart = new Cart();
        var variant = Variant();
        cart.AddItem(variant, 2);

        cart.UpdateQuantity(variant.Id, 0);

        Assert.Empty(cart.Items);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Fulfilled, false)]
    [InlineData(OrderStatus.Paid, OrderStatus.Fulfilled, false)]   // fulfillment is shipment-driven, not a manual move
    [InlineData(OrderStatus.Paid, OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.PartiallyFulfilled, OrderStatus.Cancelled, false)] // cancellation blocked once shipped
    [InlineData(OrderStatus.PartiallyFulfilled, OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Fulfilled, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid, false)]
    public void Order_transitions_follow_the_lifecycle(OrderStatus from, OrderStatus to, bool allowed)
    {
        Assert.Equal(allowed, Order.CanTransition(from, to));
    }

    [Fact]
    public void Order_transition_throws_on_an_illegal_move()
    {
        var order = new Order { Status = OrderStatus.Pending };
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(OrderStatus.Fulfilled));
    }

    [Fact]
    public void Percentage_discount_is_applied_to_the_subtotal()
    {
        var code = new DiscountCode { Code = "TENOFF", Type = DiscountType.Percentage, Value = 10m };
        Assert.Equal(new Money(5.00m, "USD"), code.ComputeDiscount(new Money(50.00m, "USD")));
    }

    [Fact]
    public void Fixed_discount_never_exceeds_the_subtotal()
    {
        var code = new DiscountCode { Code = "BIG", Type = DiscountType.FixedAmount, Value = 20m };
        Assert.Equal(new Money(8.00m, "USD"), code.ComputeDiscount(new Money(8.00m, "USD")));
    }

    [Fact]
    public void Discount_is_not_redeemable_once_expired_or_over_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = new DiscountCode { EndsAt = now.AddDays(-1) };
        var exhausted = new DiscountCode { UsageLimit = 1, TimesUsed = 1 };
        var live = new DiscountCode { EndsAt = now.AddDays(1) };

        Assert.False(expired.IsRedeemable(now));
        Assert.False(exhausted.IsRedeemable(now));
        Assert.True(live.IsRedeemable(now));
    }

    [Fact]
    public void Inventory_reserve_rejects_more_than_is_available()
    {
        var item = new InventoryItem { OnHand = 5, Reserved = 0 };
        Assert.True(item.CanReserve(5));
        Assert.False(item.CanReserve(6));
        Assert.Throws<InvalidOperationException>(() => item.Reserve(6));
    }

    [Fact]
    public void Inventory_commit_reduces_on_hand_and_reserved()
    {
        var item = new InventoryItem { OnHand = 5, Reserved = 3 };
        item.Commit(3);
        Assert.Equal(2, item.OnHand);
        Assert.Equal(0, item.Reserved);
    }
}
