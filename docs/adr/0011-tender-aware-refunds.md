# 0011 — Tender-aware refunds

**Status:** Accepted

## Context

A return refund is computed as the returned line totals adjusted by the order's discount and tax
(shipping excluded). But an order can be paid partly — or wholly — by a gift card. Refunding the
whole adjusted amount to the payment card **over-refunds real money** for the gift-card-funded
portion, and never gives that value back to the gift card. This was a genuine bug (fixed in the test
expansion work).

## Decision

Split the computed refund between the card and the gift card **in proportion to how the order was
paid** (`ReturnService.SplitRefundByTender`):

```
cardShare      = (grandTotal − giftCardTotal) / grandTotal
cardRefund     = refund × cardShare        (rounded to cents)
giftCardRefund = refund − cardRefund       (exact remainder)
```

Only the card share is sent to the gateway (skipped when zero); the gift-card share is restored to
the originating gift card via `GiftCard.Restore`.

## Consequences

- The card is never refunded more than it was charged; a fully gift-card-funded order refunds
  entirely back to the gift card and touches no gateway.
- Orders with **no** gift card have `cardShare = 1`, so they refund entirely to the card — existing
  refund totals are unchanged.
- Cents are conserved: `cardRefund + giftCardRefund == refund`. The return still records the full
  refund amount; the split is an internal tender allocation.
- Covered by `RefundTenderTests` (unit) and `RefundScenariosTests` (integration), and verified in the
  getting-started walkthrough (a $10-card order refunds $8.21 back to the card).
