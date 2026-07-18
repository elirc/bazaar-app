# Architecture

Bazaar is a Shopify-Lite e-commerce app: a customer **storefront** and an **admin** back
office, built as a monorepo with a .NET 10 API and a React + TypeScript client. This document
describes how the server is layered, how money and totals are computed, and the invariants the
rest of the system depends on.

## Monorepo layout

```
bazaar-app/
├─ server/                    # .NET 10 solution (Bazaar.slnx)
│  ├─ Bazaar.Domain/          # entities, value objects, ports — no framework dependencies
│  ├─ Bazaar.Infrastructure/  # EF Core DbContext, converters, migrations, seeders, adapters, services
│  ├─ Bazaar.Api/             # minimal-API endpoints, DTOs, validation, auth, composition root
│  └─ Bazaar.Tests/           # xUnit unit + WebApplicationFactory integration tests
├─ client/                    # Vite + React + TS (storefront + /admin)
├─ docs/                      # this documentation
└─ tmp/                       # gitignored scratch space
```

### Server layering

The server follows a Domain / Infrastructure / Api layering with dependencies pointing inward:

- **`Bazaar.Domain`** is framework-free. It holds the aggregates (`Product`, `Cart`, `Order`,
  `Wishlist`, `ReturnRequest`, `Shipment`, `GiftCard`, `TaxZone`, `DiscountCode`,
  `WebhookSubscription`, …), value objects (`Money`, `Address`), and **ports** such as
  `IPaymentGateway`. Business rules — quantity limits, the order lifecycle, inventory
  reserve/commit, discount/shipping/refund computation, shipment coverage — live here and are
  unit-tested in isolation.
- **`Bazaar.Infrastructure`** owns EF Core (`BazaarDbContext`, value converters, migrations,
  seeders), adapters (`FakePaymentGateway`, `FakeWebhookSender`, JWT/password/tax/webhook
  services), and the orchestration services `CheckoutService`, `ReturnService`,
  `FulfillmentService`, and `WebhookDispatcher`.
- **`Bazaar.Api`** is a thin minimal-API layer: endpoint groups, request/response DTOs,
  DataAnnotations validation, JWT auth + role policies, rate limiting, request logging, and
  RFC 7807 ProblemDetails via a global exception handler.

The client is a Vite + React 19 + TypeScript (strict) SPA. React Router splits a public
**storefront** from an **`/admin`** back office; a typed `apiRequest` wrapper and TanStack Query
handle data fetching.

## Money and the cents design

`Money` (`Bazaar.Domain/Common/Money.cs`) is an immutable value object carrying a `decimal
Amount` and a 3-letter ISO `Currency`. On construction the amount is rounded to two places with
banker's rounding (`MidpointRounding.ToEven`), and `ToCents()` rounds the same way.

Money is **persisted as integer minor units (cents, a `long`)** through
`DecimalToCentsConverter`, not as a SQLite `REAL`/`TEXT`, so there is no binary-floating-point or
decimal-text drift. Each owned `Money` navigation maps to two columns — `{Prefix}Amount` (cents)
and `{Prefix}Currency` — configured by the `MapMoney` helper in `BazaarDbContext`.

> **The no-shared-instance rule.** EF Core owned-type tracking keys on the *object instance*. If
> the same `Money` instance is assigned to two owned navigations on the same entity (for example a
> gift card's `InitialBalance` and `Balance`, or an order's `GrandTotal` and `GiftCardTotal`), EF
> throws. **Always construct a fresh `Money`** for each owned navigation. `GiftCard.AmountToApply`
> and the checkout/refund code deliberately build distinct instances for this reason.

Related persistence conventions:

- **`DateTimeOffset` → UTC ticks (`long`)** via `DateTimeOffsetToUtcTicksConverter`, applied
  globally in `ConfigureConventions`. SQLite cannot order or compare `DateTimeOffset` text
  correctly; storing UTC ticks makes `OrderBy`/`Where` translate to correct SQL.
- **Guid primary keys are assigned in the domain** (entity initializers), so every Guid PK is
  configured `ValueGeneratedNever`. This ensures a new child added to an already-tracked aggregate
  (e.g. a cart line) is `INSERT`ed rather than mistaken for an existing row and `UPDATE`d.
- Enums persist as their **names** (`HaveConversion<string>`), and `VariantOption` /
  `TaxCategoryRate` use explicit **composite keys** (`VariantId,Name` / `TaxZoneId,Category`) to
  avoid a shadow int PK SQLite won't auto-populate.

## The totals pipeline and tender ordering

Checkout (`CheckoutService.CheckoutAsync`) turns an open cart into a paid order. Totals are
computed in a fixed order, and payment tenders apply in a fixed order:

1. **Subtotal** — sum of each active (non-saved-for-later) line's `UnitPrice × Quantity`.
2. **Tax** — `ZoneTaxService` matches the shipping address to a `TaxZone` (an exact
   country+region zone first, else a country-wide region-less zone) and applies the zone's
   per-category rate to **each line's pre-discount amount**. With no matching zone it uses the
   legacy flat rate (`8.25%`), so region-less addresses reproduce earlier order totals.
3. **Shipping** — the requested `ShippingMethod` (or the default method when none is chosen) prices
   the cart. Methods are `Flat`, `Weight` (base + per-kg surcharge), or `FreeOverThreshold` (free at
   or above a subtotal threshold, else a flat fee). Empty carts always ship free. The seeded default
   "Standard" reproduces the legacy fixed calculator (flat `$5.99`, free at/above `$75`).
4. **Discount** — a `DiscountCode` (percentage or fixed amount), validated for redeemability, and
   **capped so it never exceeds the subtotal**.
5. **Grand total** = `subtotal + tax + shipping − discount`.
6. **Gift card (tendered last)** — a valid gift card covers `min(balance, grandTotal)`. This is a
   *tender*, not a total component: it reduces the amount charged to the card. `amountToCharge =
   grandTotal − giftCardApplied`.

The order stores `Subtotal`, `DiscountTotal`, `TaxTotal`, `ShippingTotal`, `GrandTotal`, and
`GiftCardTotal` (each a distinct `Money`). Cents are conserved: `GrandTotal.cents ==
Subtotal − Discount + Tax + Shipping` in cents.

### Reserve → charge → commit

Checkout uses a reserve-then-commit flow with the database write as the commit boundary:

1. Load the cart and its variants; validate every active line has enough stock.
2. **Reserve** stock in memory (`InventoryItem.Reserve`) — nothing is persisted yet.
3. Compute tax, shipping, discount, grand total, and the gift-card tender.
4. **Charge** the payment gateway for `amountToCharge`, but only when it is positive (a gift card
   that covers the whole total skips the gateway entirely).
5. On a successful charge, **commit**: reduce inventory on-hand/reserved (`InventoryItem.Commit`),
   redeem the gift card, mark the discount used, transition the order to `Paid`, and mark the cart
   `Converted`. A single `SaveChangesAsync` persists all of it.
6. On a declined charge the method returns early **without** saving, so the in-memory reservation
   evaporates and no partial state is written.

## Order lifecycle and shipment-derived status

Manual lifecycle transitions are defined by `Order.CanTransition`:

```
Pending            -> Paid | Cancelled
Paid               -> Refunded | Cancelled
PartiallyFulfilled -> Refunded
Fulfilled          -> Refunded
```

Fulfillment is **not** a manual transition. `FulfillmentService.CreateShipmentAsync` records a
partial shipment (guarding against over-shipping a line), then derives status from cumulative
coverage: **`Fulfilled`** when every line is fully shipped, **`PartiallyFulfilled`** when some
quantity has shipped. Consequences:

- **Cancellation is blocked once anything ships** — `PartiallyFulfilled`/`Fulfilled` can only move
  to `Refunded`.
- A **paid or partially-fulfilled** order can still be shipped; a cancelled or refunded one cannot.
- The admin transition endpoint restocks on `Cancelled`/`Refunded`.

## RMA / refund flow

Returns are per-line RMAs against a **fully `Fulfilled`** order (`ReturnService`):

- **Create** — validates each requested line belongs to the order and that
  `requested + already-returned ≤ ordered` (the **over-refund guard**, counting all non-rejected
  prior returns). Creates a `Requested` return.
- **Approve** — computes the refund, issues it, restocks the returned quantities, and marks the
  return `Approved`.
- **Reject** — marks the return `Rejected` with an optional reason; no refund, no restock.

**Refund amount** (`ComputeRefund`) is the returned line totals adjusted by the order's discount
and tax *in proportion to the returned share of the subtotal*; shipping is not refunded on a
partial return, and the result never goes negative.

**Tender-aware split** (`SplitRefundByTender`) then divides that refund between the payment card
and the gift card **in proportion to how the order was paid**:

```
cardShare      = (grandTotal − giftCardTotal) / grandTotal
cardRefund     = refund × cardShare              (rounded to cents)
giftCardRefund = refund − cardRefund             (exact remainder)
```

Only the card share hits the gateway (skipped when zero), and the gift-card share is restored to
the originating gift card via `GiftCard.Restore`. This caps card refunds at what the card was
actually charged, so a gift-card-funded order is never over-refunded in real money, while orders
with no gift card refund entirely to the card (unchanged behaviour). Cents are conserved:
`cardRefund + giftCardRefund == refund`.

## Concurrency model

Stock and carts use **optimistic concurrency**. `InventoryItem` and `Cart` implement
`IConcurrencyStamped`; `BazaarDbContext.SaveChanges[Async]` refreshes the `ConcurrencyStamp` of
every *modified* stamped entity, and the stamp is mapped as an EF concurrency token. A stale write
(two requests racing on the same row) raises `DbUpdateConcurrencyException`, which the global
exception handler maps to **HTTP 409** with a ProblemDetails body. This protects the "last unit of
stock" race and stale cart updates.

## Webhook delivery

`WebhookDispatcher.DispatchAsync(eventType, data)` fans an order-lifecycle event
(`order.created`, `order.paid`, `order.fulfilled`, `order.refunded`) out to every **active
subscription that subscribes to that event**:

1. Serialize a camelCase JSON payload `{ event, timestamp, data }`.
2. **HMAC-SHA256 sign** it with the subscription's secret (lowercase hex).
3. Deliver through `IWebhookSender` with up to `MaxAttempts = 3` retries until success.
4. Record every attempt in the delivery log (`WebhookDelivery`: success, response status, attempt
   count).

Delivery is **best-effort** — a failing endpoint is retried up to the cap and logged, and never
breaks the caller's request. The default `FakeWebhookSender` is deterministic (URLs containing
`fail` always error, so retry capping is exercisable); a production adapter would POST the payload
over HTTP with the signature header.

## Auth and roles

Authentication is stateless JWT (HS256). `JwtTokenService` hand-builds a compact token whose
payload carries `sub` (customer id), `email`, the **standard `ClaimTypes.Role` claim**, `name`,
and the registered `iss`/`aud`/`iat`/`nbf`/`exp` claims, signed with a symmetric key shared with
the API's JwtBearer validation. Notes:

- `NameClaimType` is `sub`; role checks use the default `RoleClaimType` (`ClaimTypes.Role`). The
  admin policy is `RequireAuthenticatedUser().RequireRole("Admin")`. Authentication alone is not
  authorization — a valid token without the admin role is **403**, a guest is **401**.
- **Guest cart tokens.** A cart is identified by an opaque `Token` (a `Guid` "N" string), so guests
  can shop and check out without an account. A signed-in shopper's cart is tagged with their
  `CustomerId`; the checkout attaches the resulting order to that account.
- Cross-account reads return **404** rather than 403 (no existence leak): a customer can only read
  their own orders, addresses, wishlists, and returns.
- Passwords are hashed with **PBKDF2-SHA256** (100k iterations, per-password salt) and verified in
  constant time.

## Security posture

`GHSA-2m69-gcr7-jv3q` (the SQLite native bundle) is resolved by pinning
`SQLitePCLRaw.bundle_e_sqlite3` to `3.0.3` in `Bazaar.Infrastructure`; the NuGet audit is clean
with no suppression. See `docs/adr/0009-sqlitepclraw-pin.md`.
