using Bazaar.Domain.Common;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Returns;
using Bazaar.Infrastructure.Returns;

namespace Bazaar.Tests.Domain;

/// <summary>
/// Unit tests for refund arithmetic: discount/tax proration across a multi-line partial return, and the
/// tender split that keeps a return from over-refunding real money to the card on a gift-card-funded order.
/// </summary>
public class RefundTenderTests
{
    private static Money Usd(decimal amount) => new(amount, "USD");

    private static Order OrderWith(Money grand, Money giftCard) => new()
    {
        Currency = "USD",
        GrandTotal = grand,
        GiftCardTotal = giftCard,
    };

    [Fact]
    public void ComputeRefund_prorates_discount_and_tax_across_a_multi_line_partial_return()
    {
        var lineA = new OrderLineItem { Sku = "A", Title = "A", Quantity = 2, UnitPrice = Usd(30m), LineTotal = Usd(60m) };
        var lineB = new OrderLineItem { Sku = "B", Title = "B", Quantity = 1, UnitPrice = Usd(40m), LineTotal = Usd(40m) };
        var order = new Order { Currency = "USD", Subtotal = Usd(100m), DiscountTotal = Usd(10m), TaxTotal = Usd(8m) };
        order.AddItem(lineA);
        order.AddItem(lineB);

        var request = new ReturnRequest { OrderId = order.Id };
        request.AddLine(new ReturnLine { OrderLineItemId = lineA.Id, Quantity = 1 }); // 30
        request.AddLine(new ReturnLine { OrderLineItemId = lineB.Id, Quantity = 1 }); // 40

        // returnedSubtotal 70; ratio 0.7 -> discountShare 7, taxShare 5.6 -> 70 - 7 + 5.6 = 68.60
        Assert.Equal(Usd(68.60m), ReturnService.ComputeRefund(order, request));
    }

    [Fact]
    public void ComputeRefund_when_a_discount_fully_absorbs_the_line_refunds_only_the_tax_share()
    {
        var line = new OrderLineItem { Sku = "A", Title = "A", Quantity = 1, UnitPrice = Usd(50m), LineTotal = Usd(50m) };
        var order = new Order { Currency = "USD", Subtotal = Usd(50m), DiscountTotal = Usd(50m), TaxTotal = Usd(4m) };
        order.AddItem(line);

        var request = new ReturnRequest { OrderId = order.Id };
        request.AddLine(new ReturnLine { OrderLineItemId = line.Id, Quantity = 1 });

        // 50 - 50 discount + 4 tax = 4.00 (never negative)
        Assert.Equal(Usd(4m), ReturnService.ComputeRefund(order, request));
    }

    [Fact]
    public void Refund_with_no_gift_card_is_charged_entirely_to_the_card()
    {
        var order = OrderWith(grand: Usd(100m), giftCard: Money.Zero());
        var (card, giftCard) = ReturnService.SplitRefundByTender(order, Usd(40m));

        Assert.Equal(Usd(40m), card);
        Assert.Equal(Usd(0m), giftCard);
    }

    [Fact]
    public void Refund_on_a_fully_gift_card_funded_order_goes_entirely_to_the_gift_card()
    {
        var order = OrderWith(grand: Usd(36.30m), giftCard: Usd(36.30m));
        var (card, giftCard) = ReturnService.SplitRefundByTender(order, Usd(30.31m));

        Assert.Equal(Usd(0m), card);            // nothing was charged to the card, so nothing is refunded to it
        Assert.Equal(Usd(30.31m), giftCard);
    }

    [Fact]
    public void Refund_splits_proportionally_between_card_and_gift_card_and_conserves_cents()
    {
        var order = OrderWith(grand: Usd(36.30m), giftCard: Usd(10m)); // card funded 26.30 of 36.30
        var refund = Usd(30.31m);
        var (card, giftCard) = ReturnService.SplitRefundByTender(order, refund);

        // cardShare = 26.30/36.30; 30.31 * cardShare = 21.96, remainder 8.35 to the gift card
        Assert.Equal(Usd(21.96m), card);
        Assert.Equal(Usd(8.35m), giftCard);
        Assert.Equal(refund, card + giftCard);                 // no cents created or lost
        Assert.True(card.Amount <= 36.30m - 10m);              // never exceeds what the card was charged
    }

    [Fact]
    public void A_zero_refund_splits_to_zero_on_both_tenders()
    {
        var order = OrderWith(grand: Usd(50m), giftCard: Usd(20m));
        var (card, giftCard) = ReturnService.SplitRefundByTender(order, Money.Zero());

        Assert.Equal(Usd(0m), card);
        Assert.Equal(Usd(0m), giftCard);
    }

    [Fact]
    public void Restoring_a_gift_card_adds_the_refunded_value_back_to_the_balance()
    {
        var card = new Bazaar.Domain.GiftCards.GiftCard { Code = "GC", Balance = Usd(5m), InitialBalance = Usd(25m) };
        card.Restore(Usd(8.35m));
        Assert.Equal(Usd(13.35m), card.Balance);
        Assert.True(card.IsRedeemable);
    }
}
