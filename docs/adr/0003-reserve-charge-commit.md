# 0003 — Reserve stock, charge, then commit

**Status:** Accepted

## Context

Checkout must not oversell stock and must not leave stock decremented if payment fails. A single
naive "decrement then charge" risks a phantom decrement on a declined card; "charge then decrement"
risks overselling under concurrency.

## Decision

Checkout uses a reserve-then-commit flow with the database write as the commit boundary:

1. Validate every active line has enough stock.
2. **Reserve** in memory (`InventoryItem.Reserve`) — nothing persisted yet.
3. Compute totals and the gift-card tender.
4. **Charge** the gateway for the remainder (only when positive).
5. On success, **commit**: `InventoryItem.Commit`, redeem gift card, mark discount used, transition
   the order to `Paid`, mark the cart `Converted`, then a single `SaveChangesAsync`.
6. On a decline, return early **without** saving — the in-memory reservation evaporates.

## Consequences

- Either the whole order (inventory + payment side effects) persists, or nothing does.
- The optimistic-concurrency stamp on `InventoryItem` (ADR 0010) guards the last-unit race at the
  commit; a losing writer gets a `409`.
- Reservations are per-request and in-memory; there is no long-lived reservation/expiry system.
