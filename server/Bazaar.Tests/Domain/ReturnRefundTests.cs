using Bazaar.Domain.Common;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Returns;
using Bazaar.Infrastructure.Returns;

namespace Bazaar.Tests.Domain;

public class ReturnRefundTests
{
    private static Money Usd(decimal amount) => new(amount, "USD");

    [Fact]
    public void Refund_is_adjusted_for_discount_and_tax_in_proportion_to_the_returned_share()
    {
        var line = new OrderLineItem { Sku = "SKU-1", Title = "Thing", Quantity = 2, UnitPrice = Usd(50m), LineTotal = Usd(100m) };
        var order = new Order
        {
            Currency = "USD",
            Subtotal = Usd(100m),
            DiscountTotal = Usd(20m),
            TaxTotal = Usd(8m),
            ShippingTotal = Usd(5.99m),
        };
        order.AddItem(line);

        var request = new ReturnRequest { OrderId = order.Id };
        request.AddLine(new ReturnLine { OrderLineItemId = line.Id, Quantity = 1 }); // half of the line

        // returnedSubtotal 50; ratio 0.5 -> discountShare 10, taxShare 4 -> 50 - 10 + 4 = 44
        Assert.Equal(Usd(44m), ReturnService.ComputeRefund(order, request));
    }

    [Fact]
    public void Refund_ignores_shipping()
    {
        var line = new OrderLineItem { Sku = "SKU-1", Title = "Thing", Quantity = 1, UnitPrice = Usd(40m), LineTotal = Usd(40m) };
        var order = new Order { Currency = "USD", Subtotal = Usd(40m), DiscountTotal = Money.Zero(), TaxTotal = Money.Zero(), ShippingTotal = Usd(9.99m) };
        order.AddItem(line);

        var request = new ReturnRequest { OrderId = order.Id };
        request.AddLine(new ReturnLine { OrderLineItemId = line.Id, Quantity = 1 });

        Assert.Equal(Usd(40m), ReturnService.ComputeRefund(order, request));
    }
}
