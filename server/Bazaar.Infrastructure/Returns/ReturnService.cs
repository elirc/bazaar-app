using Bazaar.Domain;
using Bazaar.Domain.Common;
using Bazaar.Domain.Orders;
using Bazaar.Domain.Payments;
using Bazaar.Domain.Returns;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Infrastructure.Returns;

public sealed record ReturnLineCommand(Guid OrderLineItemId, int Quantity);

public sealed record CreateReturnCommand(Guid OrderId, Guid? CustomerId, string? Reason, IReadOnlyList<ReturnLineCommand> Lines);

public enum ReturnCreateStatus { Ok, OrderNotFound, NotFulfilled, InvalidLine, OverRefund, NoLines }

public sealed record CreateReturnOutcome(ReturnCreateStatus Status, ReturnRequest? Return, string? Detail)
{
    public static CreateReturnOutcome Ok(ReturnRequest request) => new(ReturnCreateStatus.Ok, request, null);
    public static CreateReturnOutcome Fail(ReturnCreateStatus status, string detail) => new(status, null, detail);
}

public enum ReturnDecisionStatus { Ok, NotFound, NotPending, RefundFailed }

public sealed record ReturnDecisionOutcome(ReturnDecisionStatus Status, ReturnRequest? Return, string? Detail)
{
    public static ReturnDecisionOutcome Ok(ReturnRequest request) => new(ReturnDecisionStatus.Ok, request, null);
    public static ReturnDecisionOutcome Fail(ReturnDecisionStatus status, string detail) => new(status, null, detail);
}

/// <summary>
/// Orchestrates return/refund requests: validates per-line quantities against what remains returnable
/// (over-refund guard), and on approval issues a discount/tax-adjusted refund via the gateway and restocks.
/// </summary>
public sealed class ReturnService
{
    private readonly BazaarDbContext _db;
    private readonly IPaymentGateway _gateway;

    public ReturnService(BazaarDbContext db, IPaymentGateway gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public async Task<CreateReturnOutcome> CreateAsync(CreateReturnCommand command, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId
                && (command.CustomerId == null || o.CustomerId == command.CustomerId), ct);
        if (order is null)
            return CreateReturnOutcome.Fail(ReturnCreateStatus.OrderNotFound, "Order not found.");
        if (order.Status != OrderStatus.Fulfilled)
            return CreateReturnOutcome.Fail(ReturnCreateStatus.NotFulfilled, "Only fulfilled orders can be returned.");

        var requested = command.Lines.Where(l => l.Quantity > 0).ToList();
        if (requested.Count == 0)
            return CreateReturnOutcome.Fail(ReturnCreateStatus.NoLines, "A return must include at least one line.");

        // Quantity already consumed by non-rejected returns, per order line.
        var priorReturns = await _db.ReturnRequests
            .Where(r => r.OrderId == order.Id && r.Status != ReturnStatus.Rejected)
            .SelectMany(r => r.Lines)
            .ToListAsync(ct);
        var alreadyReturned = priorReturns
            .GroupBy(l => l.OrderLineItemId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var request = new ReturnRequest
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Reason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason!.Trim(),
            Status = ReturnStatus.Requested,
        };

        foreach (var line in requested)
        {
            var orderLine = order.Items.FirstOrDefault(i => i.Id == line.OrderLineItemId);
            if (orderLine is null)
                return CreateReturnOutcome.Fail(ReturnCreateStatus.InvalidLine, "A requested line is not part of this order.");

            var prior = alreadyReturned.TryGetValue(orderLine.Id, out var q) ? q : 0;
            if (line.Quantity + prior > orderLine.Quantity)
                return CreateReturnOutcome.Fail(ReturnCreateStatus.OverRefund,
                    $"Cannot return {line.Quantity} of '{orderLine.Sku}': only {orderLine.Quantity - prior} remain returnable.");

            request.AddLine(new ReturnLine
            {
                OrderLineItemId = orderLine.Id,
                VariantId = orderLine.VariantId,
                Sku = orderLine.Sku,
                Title = orderLine.Title,
                Quantity = line.Quantity,
            });
        }

        _db.ReturnRequests.Add(request);
        await _db.SaveChangesAsync(ct);
        return CreateReturnOutcome.Ok(request);
    }

    public async Task<ReturnDecisionOutcome> ApproveAsync(Guid returnId, CancellationToken ct = default)
    {
        var request = await _db.ReturnRequests.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == returnId, ct);
        if (request is null)
            return ReturnDecisionOutcome.Fail(ReturnDecisionStatus.NotFound, "Return not found.");
        if (request.Status != ReturnStatus.Requested)
            return ReturnDecisionOutcome.Fail(ReturnDecisionStatus.NotPending, "This return has already been decided.");

        var order = await _db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == request.OrderId, ct);
        var refund = ComputeRefund(order, request);

        var result = await _gateway.RefundAsync(new RefundRequest(order.Number, refund, order.Email), ct);
        if (!result.Succeeded)
            return ReturnDecisionOutcome.Fail(ReturnDecisionStatus.RefundFailed, result.FailureReason ?? "Refund failed.");

        // Restock the returned quantities.
        var variantIds = request.Lines.Where(l => l.VariantId.HasValue).Select(l => l.VariantId!.Value).ToList();
        var inventory = await _db.InventoryItems.Where(i => variantIds.Contains(i.VariantId)).ToListAsync(ct);
        var byVariant = inventory.ToDictionary(i => i.VariantId);
        foreach (var line in request.Lines.Where(l => l.VariantId.HasValue))
            if (byVariant.TryGetValue(line.VariantId!.Value, out var stock))
                stock.OnHand += line.Quantity;

        request.Status = ReturnStatus.Approved;
        request.RefundAmount = refund;
        request.RefundReference = result.RefundId;
        request.Touch();

        await _db.SaveChangesAsync(ct);
        return ReturnDecisionOutcome.Ok(request);
    }

    public async Task<ReturnDecisionOutcome> RejectAsync(Guid returnId, string? reason, CancellationToken ct = default)
    {
        var request = await _db.ReturnRequests.FirstOrDefaultAsync(r => r.Id == returnId, ct);
        if (request is null)
            return ReturnDecisionOutcome.Fail(ReturnDecisionStatus.NotFound, "Return not found.");
        if (request.Status != ReturnStatus.Requested)
            return ReturnDecisionOutcome.Fail(ReturnDecisionStatus.NotPending, "This return has already been decided.");

        request.Status = ReturnStatus.Rejected;
        if (!string.IsNullOrWhiteSpace(reason)) request.Reason = reason!.Trim();
        request.Touch();
        await _db.SaveChangesAsync(ct);
        return ReturnDecisionOutcome.Ok(request);
    }

    /// <summary>
    /// Refund = returned line totals, adjusted by the order's discount and tax in proportion to the
    /// returned share of the subtotal. Shipping is not refunded on a partial return.
    /// </summary>
    public static Money ComputeRefund(Order order, ReturnRequest request)
    {
        var currency = order.Currency;
        var byLine = order.Items.ToDictionary(i => i.Id);

        var returnedSubtotal = 0m;
        foreach (var line in request.Lines)
            if (byLine.TryGetValue(line.OrderLineItemId, out var orderLine))
                returnedSubtotal += orderLine.UnitPrice.Amount * line.Quantity;

        if (returnedSubtotal <= 0m)
            return Money.Zero(currency);

        if (order.Subtotal.Amount <= 0m)
            return new Money(returnedSubtotal, currency);

        var ratio = returnedSubtotal / order.Subtotal.Amount;
        var discountShare = order.DiscountTotal.Amount * ratio;
        var taxShare = order.TaxTotal.Amount * ratio;
        var refund = returnedSubtotal - discountShare + taxShare;
        return new Money(refund < 0m ? 0m : refund, currency);
    }
}
