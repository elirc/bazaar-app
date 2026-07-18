# 0010 — Optimistic concurrency on stock and carts

**Status:** Accepted

## Context

Two requests can race on the same row — most importantly the last unit of stock, and concurrent cart
updates. Pessimistic locking is heavy and awkward over a stateless HTTP API on SQLite.

## Decision

Use **optimistic concurrency**. `InventoryItem` and `Cart` implement `IConcurrencyStamped` with a
`ConcurrencyStamp` (Guid) mapped as an EF concurrency token. `BazaarDbContext.SaveChanges[Async]`
refreshes the stamp of every *modified* stamped entity before saving, so a writer working from a
stale copy fails the token check.

## Consequences

- A stale write raises `DbUpdateConcurrencyException`, which the global exception handler maps to a
  **`409 Conflict`** ProblemDetails ("please retry").
- The last-unit-of-stock race resolves to exactly one winner; the loser retries.
- Only stock and carts are stamped. Higher-level flows (e.g. gift-card redemption) rely on the
  single-writer commit within a request rather than a per-row token; a spent gift card is rejected on
  the next attempt because its balance is zero.
