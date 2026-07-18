using Bazaar.Domain.Carts;
using Bazaar.Domain.Common;
using Bazaar.Domain.Customers;
using Bazaar.Domain.Discounts;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Returns;
using Bazaar.Domain.Shipping;

namespace Bazaar.Api.Contracts;

public static class CommerceMappings
{
    public static ReturnRequestDto ToDto(this ReturnRequest r, string orderNumber) => new(
        r.Id, r.OrderId, orderNumber, r.Status.ToString(), r.Reason,
        r.RefundAmount.ToDto(),
        r.Lines.Select(l => new ReturnLineDto(l.OrderLineItemId, l.Sku, l.Title, l.Quantity)).ToList(),
        r.CreatedAt);

    public static AdminReturnDto ToAdminDto(this ReturnRequest r, string orderNumber, string email) => new(
        r.Id, r.OrderId, orderNumber, email, r.Status.ToString(), r.Reason,
        r.RefundAmount.ToDto(),
        r.Lines.Select(l => new ReturnLineDto(l.OrderLineItemId, l.Sku, l.Title, l.Quantity)).ToList(),
        r.CreatedAt);

    public static ShippingOptionDto ToOptionDto(this ShippingMethod method, Money cost) => new(
        method.Code,
        method.Name,
        method.RateType.ToString(),
        cost.ToDto(),
        method.DeliveryEstimate,
        method.MinDays,
        method.MaxDays);

    public static CustomerAddressDto ToDto(this CustomerAddress address) =>
        new(address.Id, address.Label, address.IsDefault, address.Address.ToDto());

    public static DiscountDto ToDto(this DiscountCode code) => new(
        code.Id,
        code.Code,
        code.Type.ToString(),
        code.Value,
        code.Currency,
        code.IsActive,
        code.StartsAt,
        code.EndsAt,
        code.UsageLimit,
        code.TimesUsed);


    public static CartDto ToDto(this Cart cart, IReadOnlyDictionary<Guid, int> availability)
    {
        var currency = cart.Items.Count > 0
            ? cart.Items[0].Variant!.Price.Currency
            : Money.DefaultCurrency;

        var lines = cart.Items
            .OrderBy(i => i.Variant?.Product?.Title)
            .Select(i =>
            {
                var variant = i.Variant!;
                var lineTotal = variant.Price.MultiplyBy(i.Quantity);
                return new CartLineDto(
                    variant.Id,
                    variant.Product?.Slug ?? string.Empty,
                    variant.Product?.Title ?? variant.Title,
                    variant.Title,
                    variant.Sku,
                    variant.Price.ToDto(),
                    i.Quantity,
                    lineTotal.ToDto(),
                    availability.TryGetValue(variant.Id, out var qty) ? qty : 0,
                    i.SavedForLater);
            })
            .ToList();

        // Saved-for-later lines are excluded from the subtotal and the active item count.
        var active = cart.Items.Where(i => !i.SavedForLater).ToList();
        var subtotal = active.Aggregate(
            Money.Zero(currency),
            (acc, i) => acc.Add(i.Variant!.Price.MultiplyBy(i.Quantity)));

        return new CartDto(
            cart.Id,
            cart.Token,
            lines,
            subtotal.ToDto(),
            active.Sum(i => i.Quantity),
            cart.Items.Count(i => i.SavedForLater));
    }

    public static AddressDto ToDto(this Address address) =>
        new(address.Name, address.Line1, address.Line2, address.City, address.Region, address.PostalCode, address.Country);

    public static Address ToAddress(this AddressInput input) => new()
    {
        Name = input.Name!,
        Line1 = input.Line1!,
        Line2 = input.Line2,
        City = input.City!,
        Region = input.Region,
        PostalCode = input.PostalCode!,
        Country = input.Country!.ToUpperInvariant(),
    };

    public static OrderDto ToDto(this Order order, IEnumerable<Shipment>? shipments = null) => new(
        order.Id,
        order.Number,
        order.Email,
        order.Status.ToString(),
        order.Currency,
        order.ShippingAddress.ToDto(),
        order.Subtotal.ToDto(),
        order.DiscountTotal.ToDto(),
        order.TaxTotal.ToDto(),
        order.ShippingTotal.ToDto(),
        order.GrandTotal.ToDto(),
        order.DiscountCode,
        order.ShippingMethod,
        order.GiftCardTotal.ToDto(),
        order.GiftCardCode,
        order.Items.Select(li => new OrderLineDto(li.Id, li.VariantId, li.Sku, li.Title, li.Quantity, li.UnitPrice.ToDto(), li.LineTotal.ToDto())).ToList(),
        order.PlacedAt,
        (shipments ?? Enumerable.Empty<Shipment>())
            .OrderBy(s => s.ShippedAt)
            .Select(s => new ShipmentDto(
                s.Id, s.Carrier, s.TrackingNumber, s.ShippedAt,
                s.Lines.Select(l => new ShipmentLineDto(l.OrderLineItemId, l.Sku, l.Title, l.Quantity)).ToList()))
            .ToList());

    public static OrderSummaryDto ToSummaryDto(this Order order) => new(
        order.Id,
        order.Number,
        order.Email,
        order.Status.ToString(),
        order.GrandTotal.ToDto(),
        order.Items.Sum(li => li.Quantity),
        order.PlacedAt);
}
