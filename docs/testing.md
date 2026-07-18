# Testing

Bazaar has **217 server tests** (xUnit) and **38 client tests** (Vitest + React Testing Library).
The four gates are: server tests, client tests, client typecheck (`tsc -b`), client lint (`oxlint`).

## How to run

```bash
# Server
cd server
dotnet test                                          # all 217

# Client
cd client
pnpm test                                            # all 38 (vitest run)
pnpm typecheck                                        # tsc -b
pnpm lint                                             # oxlint

# A single server test or class
dotnet test --filter "FullyQualifiedName~MigrationDriftTests"

# A single client file (definitive, isolated)
pnpm test src/routes/storefront/CheckoutPage.test.tsx
```

## Taxonomy

### Server (`Bazaar.Tests`)

- **Domain unit tests** (`Domain/`) — pure, no I/O: `Money` rounding/round-trips, the order
  lifecycle table, inventory reserve/commit, discount/shipping/tax math, webhook signing, refund
  proration, and the refund **tender split** (`RefundTenderTests`).
- **Persistence tests** (`Persistence/`) — against a migrated in-memory SQLite DB: Money and
  `DateTimeOffset` round-trips, `DateTimeOffset` ordering/filtering in SQL, aggregate graphs, and
  **optimistic-concurrency conflicts** (`ConcurrencyTests`).
- **Migration-drift guard** (`Persistence/MigrationDriftTests`) — see below.
- **Endpoint integration tests** (`Endpoints/`) — full HTTP round-trips through
  `WebApplicationFactory`: catalog, cart, checkout, the totals pipeline, orders and the state
  machine, fulfillment, returns and refund scenarios, reviews, wishlists, tax/gift cards,
  reporting, webhooks, the authorization matrix, and hardening/edge cases.

### Client

- **Component tests** (`*.test.tsx`) — Vitest + React Testing Library in a jsdom environment,
  with `fetch` stubbed. They cover auth-gated admin routes, the checkout flow (discount + gift
  card), the RMA request flow, the review moderation queue, cart/pagination/format helpers, and
  the typed API client.

## Harnesses

- **`TestDb`** — a disposable, isolated in-memory SQLite database. The connection is held open for
  the handle's lifetime so the schema (created by applying the **real migrations** via
  `Database.Migrate()`) survives across contexts. Used by persistence, concurrency, and drift tests.
- **`BazaarApiFactory`** — a `WebApplicationFactory<Program>` that boots the API against a private
  in-memory SQLite connection, so integration tests never touch a real file and each factory
  instance is fully isolated. `IClassFixture<BazaarApiFactory>` shares one database across a test
  class; classes that must observe a pristine state (e.g. empty reports) simply never place an order.
- **`AuthTestExtensions`** — `RegisterAsync`/`LoginAsync`, `UseBearer`, and `AuthenticateAdminAsync`
  (logs in as the seeded admin and attaches the bearer token).
- **Client `renderWithProviders`** — wraps a component in a fresh `QueryClient` + `MemoryRouter`;
  `jsonResponse` builds stub `Response`s and tests drive `fetch` via `vi.stubGlobal('fetch', …)`.

## The migration-drift guard

`MigrationDriftTests` protects against the EF model drifting away from the migrations that build a
real database:

- **`The_model_has_no_pending_changes_not_captured_in_a_migration`** diffs the live design-time
  relational model against the latest migration **snapshot** (via `IMigrationsModelDiffer` +
  `IMigrationsAssembly`). Any operations mean a migration is missing and the test fails with the
  pending operation names.
- **`All_seeders_run_cleanly_on_a_freshly_migrated_database`** runs every seeder (catalog, shipping,
  tax/gift-card, accounts) against a `Migrate()`d database and re-runs them to prove idempotency.

To confirm the guard actually bites, add an unmapped scalar property to a mapped entity and run the
first test — it goes red (an `AddColumn` operation is detected). Remove the property to restore
green. This was done once during development; the model is currently in sync with the migrations
(no drift).

## Flakiness policy (client jsdom under load)

The client suite is deterministic in content but its default **forks** pool spawns a child process
per test file. On a heavily loaded machine those child workers can miss their startup handshake and
Vitest reports `Failed to start forks worker` / `Timeout waiting for worker to respond`. This is an
environment/load artifact, not a real test failure.

Mitigation:

1. Run with the lighter **threads** pool and a generous per-test timeout:
   ```bash
   pnpm test -- --pool=threads --testTimeout=30000
   ```
   Threads reuse the jsdom environment and start far faster under load, which reliably clears the
   worker-startup timeouts.
2. If a specific file still looks load-induced, **re-run that file in isolation** for a definitive
   result: `pnpm test src/.../Thing.test.tsx`. A pass in isolation is authoritative.
3. The server suite is stable; when in doubt, run `dotnet test` twice to rule out load noise.
