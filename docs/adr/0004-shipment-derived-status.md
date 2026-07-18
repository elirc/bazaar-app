# 0004 — Order fulfillment status is derived from shipment coverage

**Status:** Accepted

## Context

Fulfillment is a real-world process (goods leave in one or more shipments), not a button an
operator flips. Allowing a manual `Paid → Fulfilled` move would let the recorded status disagree
with what actually shipped.

## Decision

Fulfillment status is **derived from cumulative shipment coverage**, not from the manual transition
table. `FulfillmentService.CreateShipmentAsync` records a partial shipment (guarding against
over-shipping a line) and then sets:

- **`Fulfilled`** when every line is fully shipped,
- **`PartiallyFulfilled`** when some quantity has shipped.

`Order.CanTransition` deliberately omits any manual move into `Fulfilled`/`PartiallyFulfilled`.

## Consequences

- **Cancellation is blocked once anything ships** — shipped orders can only move to `Refunded`.
- Returns require a fully `Fulfilled` order (ADR 0011).
- `order.fulfilled` webhooks fire only when a shipment *completes* coverage.
- The order's status is always consistent with its shipment records.
