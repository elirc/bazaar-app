# Bazaar

A Shopify-Lite e-commerce application: a customer **storefront** (browse, search, cart,
checkout, accounts, reviews, wishlists, returns, tracking) and an **admin** back office
(catalog, orders & fulfilment, discounts, reviews, returns, gift cards, tax, reports &
webhooks), built as a monorepo with a .NET 10 API and a React + TypeScript client.

- **`/server`** — ASP.NET Core Web API on .NET 10, EF Core + SQLite, layered as
  `Bazaar.Api` / `Bazaar.Domain` / `Bazaar.Infrastructure` / `Bazaar.Tests`.
- **`/client`** — Vite + React + TypeScript (strict). React Router splits a public
  **storefront** from an **`/admin`** back office. Typed API client, TanStack Query,
  Vitest + React Testing Library.

## Features

**Storefront**
- Product grid with URL-driven search, collection filter, sort, pagination, and aggregate
  star ratings.
- Product detail with variant selection, live availability, add-to-cart, add-to-wishlist,
  a verified-purchase review section (with helpful votes), and shipment tracking on orders.
- Slide-out cart drawer with quantity steppers, remove, **save-for-later** (excluded from
  totals), and a live subtotal.
- Checkout with contact + shipping address, **shipping-method selection** (with delivery
  estimates), discount-code entry, **gift-card** entry, tax, and an order confirmation with
  tracking and (for fulfilled orders) a **return request** flow.
- **Accounts**: register / sign in (JWT), order history, an **address book**, **wishlists**
  (default + named, move-to-cart, back-in-stock flags), and returns history.

**Admin (`/admin`)**
- Product management (search / status filter / paging, create & edit incl. variants, stock,
  weight, tax category, collection membership), collection management.
- Order management: list, detail, lifecycle transitions, and **fulfilment** — partial
  shipments with carrier/tracking; order status is derived from shipment coverage
  (`PartiallyFulfilled` / `Fulfilled`).
- Discounts, **review moderation**, **returns queue** (approve → tender-aware refund + restock),
  **gift-card** issuance, **reports** (sales over time with inline bars, top products, low
  stock, discount usage), and **webhook** settings (subscriptions + delivery log).

## Documentation

Full docs live in [`docs/`](docs/):

- [Architecture](docs/architecture.md) — layering, the Money/cents design, the totals pipeline &
  tender ordering, shipment-derived status, the RMA/refund flow, webhooks, concurrency, auth/roles.
- [API reference](docs/api-reference.md) — every endpoint: method, route, auth, shapes, error codes.
- [Getting started](docs/getting-started.md) — run both halves and walk an order from browse to a
  refunded return (verified against a live run).
- [Testing](docs/testing.md) — taxonomy, harnesses, the migration-drift guard, and the jsdom
  flakiness policy.
- [ADRs](docs/adr/README.md) — the load-bearing decisions and why.

## Prerequisites

| Tool     | Version                      |
| -------- | ---------------------------- |
| .NET SDK | 10.0.302                     |
| Node.js  | 22.x                         |
| pnpm     | 9.x (`corepack enable pnpm`) |

> The client uses **pnpm** (shared content-addressed store) to keep disk usage low.

## Running the server

```bash
cd server
dotnet restore
dotnet run --project Bazaar.Api        # http://localhost:5180
```

On startup the API applies EF Core migrations and seeds a small dev catalog, shipping
methods, tax zones, a demo gift card, and accounts.

Health check (with DB probe): `GET http://localhost:5180/health` →
`{ "status": "ok", "service": "bazaar-api", "checks": { "database": "ok" }, ... }`

Seeded dev accounts: **admin** `admin@bazaar.test` / `admin-dev-password`, **customer**
`shopper@bazaar.test` / `shopper-dev-password`.

```bash
cd server
dotnet test        # xUnit + WebApplicationFactory integration tests
```

## Running the client

```bash
cd client
pnpm install
pnpm dev                                # http://localhost:5173
```

The Vite dev server proxies `/api` and `/health` to the API (override with `VITE_API_PROXY`).
To point a production build at a specific API origin, set `VITE_API_BASE_URL`.

Storefront: <http://localhost:5173/> · Admin: <http://localhost:5173/admin>

```bash
cd client
pnpm test          # vitest
pnpm build         # tsc -b && vite build
```

## Repository layout

```
bazaar-app/
├─ server/                    # .NET 10 solution (Bazaar.slnx)
│  ├─ Bazaar.Domain/          # entities, value objects, ports (no framework deps)
│  ├─ Bazaar.Infrastructure/  # EF Core DbContext, converters, migrations, seed, adapters, services
│  ├─ Bazaar.Api/             # HTTP endpoints, DTOs, validation, auth, composition root
│  └─ Bazaar.Tests/           # xUnit unit + WebApplicationFactory integration tests
├─ client/                    # Vite + React + TS (storefront + /admin)
└─ tmp/                       # gitignored scratch space
```

## Architecture

- **Domain** (`Bazaar.Domain`) is framework-free: aggregates (`Product`, `Cart`, `Order`,
  `Wishlist`, `ReturnRequest`, `Shipment`, `GiftCard`, `TaxZone`, `WebhookSubscription`),
  value objects (`Money`, `Address`), and **ports** (`IPaymentGateway`). Business rules —
  quantity limits, the order lifecycle, inventory reserve/commit, discount/shipping/tax and
  refund computation, shipment coverage — live here and are unit-tested in isolation.
- **Infrastructure** owns EF Core (the `BazaarDbContext`, value converters, migrations,
  seeding), adapters (`FakePaymentGateway`, `FakeWebhookSender`, JWT/password/tax/webhook
  services), and orchestration services (`CheckoutService`, `ReturnService`,
  `FulfillmentService`, `WebhookDispatcher`).
- **Api** is a thin minimal-API layer: endpoint groups, request/response DTOs, DataAnnotations
  validation, JWT auth + role policies, rate limiting, request logging, and RFC 7807
  ProblemDetails (including a global exception handler).

## API surface (selected)

| Area       | Endpoint                                                          |
| ---------- | ---------------------------------------------------------------- |
| Auth       | `POST /api/auth/register\|login`, `GET /api/auth/me`             |
| Account    | `GET /api/account/orders[/{id}]`, `…/addresses`, `…/returns`     |
|            | `GET\|POST\|DELETE /api/account/wishlists…`, `…/wishlist/items`  |
|            | `POST /api/account/orders/{id}/returns`                          |
| Storefront | `GET /api/storefront/products` (search/collection/sort/page)     |
|            | `GET /api/storefront/products/{slug}[/reviews]`, `…/collections` |
|            | `GET /api/storefront/discounts/{code}`, `…/gift-cards/{code}`    |
|            | `POST /api/storefront/products/{slug}/reviews`, `…/reviews/{id}/helpful` |
| Cart       | `POST /api/cart`, `GET /api/cart/{token}`                        |
|            | `POST\|PUT\|DELETE /api/cart/{token}/items[/{variantId}[/saved]]` |
| Checkout   | `POST /api/checkout` (rate-limited), `GET /api/orders/{id}`      |
|            | `GET /api/checkout/shipping-options`                             |
| Admin      | `…/products`, `…/variants`, `…/collections`, `…/discounts`       |
|            | `…/orders[/{id}]`, `…/orders/{id}/transition\|shipments`         |
|            | `…/reviews[/{id}/moderate]`, `…/returns[/{id}/approve\|reject]`  |
|            | `…/gift-cards`, `…/reports/{sales\|top-products\|low-stock\|discounts}` |
|            | `…/webhooks[/deliveries]`                                        |
| Ops        | `GET /health` (DB probe, structured body)                        |

## Development notes & gotchas

- **SQLite + `DateTimeOffset`:** persisted as UTC ticks (`long`) via a global value converter
  (SQLite cannot order/compare `DateTimeOffset`).
- **Money:** stored as integer minor units (cents, a `long`) plus a 3-letter currency code;
  arithmetic uses banker's rounding. **Never share one `Money` instance across two owned
  navigations** (e.g. a gift card's initial/balance, or an order's grand-total/gift-card-total)
  — EF owned-type tracking keys on the instance and will throw; construct distinct instances.
- **Guid keys:** assigned in the domain, so EF is configured `ValueGeneratedNever`.
- **Optimistic concurrency:** `InventoryItem` and `Cart` carry a `ConcurrencyStamp` refreshed on
  every update; a stale write raises `DbUpdateConcurrencyException`, mapped to a **409**.
- **Fulfillment:** order status is derived from shipment coverage (not a manual transition);
  cancellation is blocked once anything ships.
- **Refunds are tender-aware:** an approved return's refund is split between the payment card and any
  gift card in proportion to how the order was paid, so a gift-card-funded order is never
  over-refunded to the card (the gift-card share is restored to the card). See ADR 0011.
- **Tax:** matched by zone (country/region + per-category rate); region-less addresses fall back
  to the flat rate so earlier totals are preserved.
- **Webhooks:** HMAC-SHA256 signed, delivered best-effort with capped retries; the default sender
  is a deterministic fake (URLs containing "fail" error) — swap in an HTTP adapter for production.
- **Validation:** request DTOs make "required" fields nullable so a missing value yields a `400`;
  `Include` runs before `Skip`/`Take`.
- **Security audit:** `GHSA-2m69-gcr7-jv3q` is resolved by pinning `SQLitePCLRaw.bundle_e_sqlite3`
  to `3.0.3` in `Bazaar.Infrastructure`; the NuGet audit is clean with no suppression.

## Tests

- **Server:** 217 tests (xUnit) — domain behaviour (Money, lifecycle, shipping/tax/refund/webhook
  logic, tender-split refunds), EF persistence round-trips + concurrency, a **migration-drift
  guard**, and WebApplicationFactory integration tests over an in-memory SQLite database.
- **Client:** 38 tests (Vitest + React Testing Library, fetch-stubbed).

See [`docs/testing.md`](docs/testing.md) for the taxonomy, harnesses, and the jsdom flakiness policy.

## Sprint history

Shipped as merged sprint PRs on `main`.

**Phase 1 (v1.0.0):**

1. Scaffold — monorepo, `/health`, client shell, both test harnesses.
2. Domain + persistence — catalog/inventory/carts/orders entities, Money, EF migration + seed.
3. Catalog — admin product/collection CRUD + storefront browse/search/filter/pagination.
4. Cart & checkout — guest cart, checkout with tax/shipping, fake payment gateway.
5. Orders, admin & discounts — order lifecycle + admin screens, discount codes at checkout.
6. Hardening — ProblemDetails, stock/pagination/validation edge cases.

**Phase 2 (v2.0.0):**

7. Accounts & auth — customer registration/login (JWT), admin role, order history.
8. Addresses & shipping — address book, shipping methods (flat/weight/threshold), estimates.
9. Reviews & ratings — verified-purchase reviews, moderation, aggregate ratings, helpful votes.
10. Wishlists & saved-for-later — default + named lists, move-to-cart, back-in-stock flags.
11. Returns & refunds — per-line RMAs, approval flow, discount/tax-adjusted refunds, restock.
12. Tax zones & gift cards — zone/category tax rates, gift-card issue/redeem/partial tender.
13. Fulfillment — partial shipments, coverage-derived status, cancellation guards, tracking.
14. Reporting & webhooks — admin reports, HMAC-signed webhooks with delivery log + retries.
15. Production readiness — request logging, DB-probe health, optimistic concurrency, checkout
    rate limiting, pagination audit, this README.
