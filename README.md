# Bazaar

A Shopify-Lite e-commerce application: a customer **storefront** (browse, cart, checkout)
and an **admin** back office (product & order management), built as a monorepo.

- **`/server`** — ASP.NET Core Web API on .NET 10, EF Core + SQLite, layered as
  `Bazaar.Api` / `Bazaar.Domain` / `Bazaar.Infrastructure` / `Bazaar.Tests`.
- **`/client`** — Vite + React + TypeScript (strict). React Router splits a public
  **storefront** from an **`/admin`** back office. Typed API client, TanStack Query,
  Vitest + React Testing Library.

## Prerequisites

| Tool        | Version                     |
| ----------- | --------------------------- |
| .NET SDK    | 10.0.302                    |
| Node.js     | 22.x                        |
| pnpm        | 9.x (`corepack enable pnpm`)|

> The client uses **pnpm** (shared content-addressed store) to keep disk usage low.

## Running the server

```bash
cd server
dotnet restore
dotnet run --project Bazaar.Api        # http://localhost:5180
```

Health check: `GET http://localhost:5180/health` → `{ "status": "ok", "service": "bazaar-api" }`

Tests:

```bash
cd server
dotnet test
```

## Running the client

```bash
cd client
pnpm install
pnpm dev                                # http://localhost:5173
```

The Vite dev server proxies `/api` and `/health` to the API at `http://localhost:5180`
(override with the `VITE_API_PROXY` env var). To point a production build at a specific
API origin, set `VITE_API_BASE_URL`.

Storefront: <http://localhost:5173/> · Admin: <http://localhost:5173/admin>

Tests / build:

```bash
cd client
pnpm test          # vitest
pnpm build         # tsc -b && vite build
```

## Repository layout

```
bazaar-app/
├─ server/                 # .NET 10 solution (Bazaar.slnx)
│  ├─ Bazaar.Domain/        # entities, value objects, ports (no framework deps)
│  ├─ Bazaar.Infrastructure/# EF Core DbContext, converters, seed, adapters
│  ├─ Bazaar.Api/           # HTTP endpoints, DTOs, composition root
│  └─ Bazaar.Tests/         # xUnit + WebApplicationFactory integration tests
├─ client/                 # Vite + React + TS (storefront + /admin)
└─ tmp/                    # gitignored scratch space
```

## Domain

Catalog of **products** with **variants** (SKU, price, options) and **collections**;
**inventory** with stock levels and checkout reservation; guest **carts**; **checkout**
producing **orders** (address, tax + shipping totals) with a pending → paid → fulfilled →
cancelled/refunded lifecycle over a fake payment-gateway port; **discount codes**;
**customers**. Money is stored as decimal with a currency code. See per-sprint PRs for
how each slice was built.

## Development notes

- **SQLite + `DateTimeOffset`:** SQLite cannot order/compare `DateTimeOffset`, so those
  columns use a UTC-ticks (`long`) value converter.
- **Money:** stored as decimal minor units + a currency code with rounding covered by tests.
- Commit history is organised as six sprint PRs (`sprint-01` … `sprint-06`).
