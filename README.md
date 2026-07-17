# Bazaar

A Shopify-Lite e-commerce application: a customer **storefront** (browse, search, cart,
checkout) and an **admin** back office (product, collection, order & discount management),
built as a monorepo with a .NET 10 API and a React + TypeScript client.

- **`/server`** — ASP.NET Core Web API on .NET 10, EF Core + SQLite, layered as
  `Bazaar.Api` / `Bazaar.Domain` / `Bazaar.Infrastructure` / `Bazaar.Tests`.
- **`/client`** — Vite + React + TypeScript (strict). React Router splits a public
  **storefront** from an **`/admin`** back office. Typed API client, TanStack Query,
  Vitest + React Testing Library.

## Features

**Storefront**
- Product grid with URL-driven search, collection filter, sort (newest / price / name)
  and pagination.
- Product detail with variant selection, live availability, and add-to-cart.
- Slide-out cart drawer (quantity steppers, remove, live subtotal).
- Checkout with contact + shipping address, discount-code entry, tax & shipping, and an
  order confirmation page.

**Admin (`/admin`)**
- Product management: list (search / status filter / paging), create & edit (fields,
  status, collection membership, variants with inline price & stock editing), delete.
- Collection management (create / list / delete).
- Order management: list (search / status / paging), detail, and lifecycle transitions
  (Paid → Fulfilled / Refunded / Cancelled) with automatic restock on cancel/refund.
- Discount management (create / list / delete).

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

On startup the API applies EF Core migrations and seeds a small dev catalog
(6 products / 12 variants / 3 collections / 2 discount codes).

Health check: `GET http://localhost:5180/health` → `{ "status": "ok", "service": "bazaar-api" }`

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

The Vite dev server proxies `/api` and `/health` to the API at `http://localhost:5180`
(override with `VITE_API_PROXY`). To point a production build at a specific API origin, set
`VITE_API_BASE_URL`.

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
│  ├─ Bazaar.Infrastructure/  # EF Core DbContext, converters, migrations, seed, adapters
│  ├─ Bazaar.Api/             # HTTP endpoints, DTOs, validation, composition root
│  └─ Bazaar.Tests/           # xUnit unit + WebApplicationFactory integration tests
├─ client/                    # Vite + React + TS (storefront + /admin)
└─ tmp/                       # gitignored scratch space
```

## Architecture

- **Domain** (`Bazaar.Domain`) is framework-free: aggregates (`Product`, `Cart`, `Order`),
  value objects (`Money`, `Address`), and **ports** (`IPaymentGateway`, `ITaxCalculator`,
  `IShippingCalculator`). Business rules — quantity limits, the order lifecycle transition
  table, inventory reserve/commit, discount computation — live here and are unit-tested in
  isolation.
- **Infrastructure** owns EF Core (the `BazaarDbContext`, value converters, migrations,
  seeding), the `FakePaymentGateway` adapter, and the `CheckoutService` orchestration.
- **Api** is a thin minimal-API layer: endpoint groups, request/response DTOs, DataAnnotations
  validation, and RFC 7807 ProblemDetails responses (including a global exception handler).

## API surface (selected)

| Area       | Endpoint                                                        |
| ---------- | --------------------------------------------------------------- |
| Storefront | `GET /api/storefront/products` (search, collection, sort, page) |
|            | `GET /api/storefront/products/{slug}`                           |
|            | `GET /api/storefront/collections`                               |
|            | `GET /api/storefront/discounts/{code}`                          |
| Cart       | `POST /api/cart`, `GET /api/cart/{token}`                       |
|            | `POST|PUT|DELETE /api/cart/{token}/items[/{variantId}]`         |
| Checkout   | `POST /api/checkout`, `GET /api/orders/{id}`                    |
| Admin      | `GET|POST|PUT|DELETE /api/admin/products[/{id}]`                |
|            | `PUT /api/admin/variants/{id}`                                  |
|            | `GET|POST|PUT|DELETE /api/admin/collections[/{id}]`             |
|            | `GET /api/admin/orders`, `POST /api/admin/orders/{id}/transition` |
|            | `GET|POST|DELETE /api/admin/discounts[/{id}]`                   |

## Development notes & gotchas

- **SQLite + `DateTimeOffset`:** SQLite cannot order/compare `DateTimeOffset`, so every such
  column is persisted as UTC ticks (`long`) via a global value converter.
- **Money:** stored as integer minor units (cents, a `long`) plus a 3-letter currency code;
  arithmetic uses banker's rounding, covered by tests.
- **Guid keys:** primary keys are assigned in the domain, so EF is configured
  `ValueGeneratedNever` — otherwise a new line added to an already-tracked cart would be
  mistaken for an existing row and `UPDATE`d instead of `INSERT`ed.
- **Validation:** request DTOs make "required" fields nullable so a missing value yields a
  `400` (a non-nullable `Guid`/`DateTimeOffset` with `[Required]` is a no-op); `Include`
  runs before `Skip`/`Take`.
- **Security audit:** `GHSA-2m69-gcr7-jv3q` (the SQLite native library bundled transitively
  by EF Core) is resolved by pinning `SQLitePCLRaw.bundle_e_sqlite3` to `3.0.3` in
  `Bazaar.Infrastructure`; the NuGet audit is clean with no suppression.

## Tests

- **Server:** 90 tests (xUnit) — Money & domain behaviour, EF persistence round-trips,
  and WebApplicationFactory integration tests over an in-memory SQLite database.
- **Client:** 19 tests (Vitest + React Testing Library, fetch-stubbed).

## Sprint history

The build shipped as six merged sprint PRs on `main`:

1. Scaffold — monorepo, `/health`, client shell, both test harnesses.
2. Domain + persistence — catalog/inventory/carts/orders entities, Money, EF migration + seed.
3. Catalog — admin product/collection CRUD + storefront browse/search/filter/pagination.
4. Cart & checkout — guest cart, checkout with tax/shipping, fake payment gateway.
5. Orders, admin & discounts — order lifecycle + admin screens, discount codes at checkout.
6. Hardening — ProblemDetails, stock/pagination/validation edge cases, tests, this README.
