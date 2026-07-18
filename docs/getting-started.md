# Getting started

Run both halves of Bazaar locally and walk an order all the way from browse to a refunded return.

## Prerequisites

| Tool     | Version                      |
| -------- | ---------------------------- |
| .NET SDK | 10.0.x                       |
| Node.js  | 22.x                         |
| pnpm     | 9.x (`corepack enable pnpm`) |

## Run the server

```bash
cd server
dotnet restore
dotnet run --project Bazaar.Api        # http://localhost:5180
```

On startup the API applies EF Core migrations and seeds a small dev catalog (6 products, 3
collections), shipping methods, tax zones, a demo gift card (`GIFT25`), and accounts. The
database is a local SQLite file (`server/Bazaar.Api/bazaar.db`); delete it to reseed from scratch.

Health check (with a DB probe):

```bash
curl http://localhost:5180/health
# {"status":"ok","service":"bazaar-api","checks":{"database":"ok"},"timestamp":"…"}
```

**Seeded accounts** — admin `admin@bazaar.test` / `admin-dev-password`, customer
`shopper@bazaar.test` / `shopper-dev-password`. **Seeded discounts** — `WELCOME10` (10% off),
`SHIP5` ($5 off).

## Run the client

```bash
cd client
pnpm install
pnpm dev                                # http://localhost:5173
```

The Vite dev server proxies `/api` and `/health` to `http://localhost:5180` (override with
`VITE_API_PROXY`). Storefront: <http://localhost:5173/> · Admin: <http://localhost:5173/admin>.

## End-to-end walkthrough (browse → checkout → fulfill → RMA refund)

The flow below is copy-pasteable against a freshly seeded server and mirrors the automated
integration tests. Amounts are the actual values the API returns.

```bash
BASE=http://localhost:5180

# 1) Browse the catalog
curl -s "$BASE/api/storefront/products?pageSize=2"

# 2) Register a customer -> capture the token
CUST=$(curl -s -X POST "$BASE/api/auth/register" -H 'Content-Type: application/json' \
  -d '{"email":"walkthrough@example.com","password":"supersecret","firstName":"Wanda"}')
CUST_TOKEN=$(echo "$CUST" | python -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 3) Find the MUG-CRM variant id from the product detail
VARID=$(curl -s "$BASE/api/storefront/products/ceramic-mug" \
  | python -c "import sys,json;d=json.load(sys.stdin);print([v['id'] for v in d['variants'] if v['sku']=='MUG-CRM'][0])")

# 4) Create a cart (as the customer) and add 2 mugs ($14 each -> $28 subtotal)
TOKEN=$(curl -s -X POST "$BASE/api/cart" -H "Authorization: Bearer $CUST_TOKEN" \
  | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
curl -s -X POST "$BASE/api/cart/$TOKEN/items" -H "Authorization: Bearer $CUST_TOKEN" \
  -H 'Content-Type: application/json' -d "{\"variantId\":\"$VARID\",\"quantity\":2}"

# 5) Sign in as admin and issue a $10 gift card
ADMIN_TOKEN=$(curl -s -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' \
  -d '{"email":"admin@bazaar.test","password":"admin-dev-password"}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['token'])")
GC_CODE=$(curl -s -X POST "$BASE/api/admin/gift-cards" -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H 'Content-Type: application/json' -d '{"amount":10}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['code'])")

# 6) Checkout with WELCOME10 (10% off) + the gift card
ORDER=$(curl -s -X POST "$BASE/api/checkout" -H "Authorization: Bearer $CUST_TOKEN" \
  -H 'Content-Type: application/json' -d "{\"cartToken\":\"$TOKEN\",\"email\":\"walkthrough@example.com\",\"discountCode\":\"WELCOME10\",\"giftCardCode\":\"$GC_CODE\",\"shippingAddress\":{\"name\":\"Wanda\",\"line1\":\"1 Market St\",\"city\":\"Denver\",\"postalCode\":\"80202\",\"country\":\"US\"}}")
echo "$ORDER"
# status "Paid", subtotal 28.00, discount 2.80, tax 2.31 (flat 8.25%), shipping 5.99,
# grandTotal 33.50, giftCardTotal 10.00  -> $23.50 charged to the card
ORDER_ID=$(echo "$ORDER" | python -c "import sys,json;print(json.load(sys.stdin)['id'])")
LINE_ID=$(echo "$ORDER" | python -c "import sys,json;print(json.load(sys.stdin)['items'][0]['id'])")

# 7) Admin fulfills with a full shipment -> order becomes "Fulfilled"
curl -s -X POST "$BASE/api/admin/orders/$ORDER_ID/shipments" -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{\"carrier\":\"UPS\",\"trackingNumber\":\"1Z-DEMO\",\"lines\":[{\"orderLineItemId\":\"$LINE_ID\",\"quantity\":2}]}"

# 8) Customer requests a return for both units (only fulfilled orders are returnable)
RMA_ID=$(curl -s -X POST "$BASE/api/account/orders/$ORDER_ID/returns" -H "Authorization: Bearer $CUST_TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{\"reason\":\"Changed my mind\",\"lines\":[{\"orderLineItemId\":\"$LINE_ID\",\"quantity\":2}]}" \
  | python -c "import sys,json;print(json.load(sys.stdin)['id'])")

# 9) Admin approves -> a discount/tax-adjusted, tender-aware refund of $27.51
curl -s -X POST "$BASE/api/admin/returns/$RMA_ID/approve" -H "Authorization: Bearer $ADMIN_TOKEN"

# 10) The gift card is restored: it was spent to $0 at checkout, then $8.21 came back on the refund
curl -s "$BASE/api/storefront/gift-cards/$GC_CODE"
# {"code":"GC-…","valid":true,"balance":{"amount":8.21,"currency":"USD"}}
```

### What the numbers mean

- **Subtotal $28.00** — 2 × Stoneware Coffee Mug at $14.00.
- **Discount −$2.80** — `WELCOME10` is 10% of the subtotal.
- **Tax $2.31** — the Denver address matches no seeded tax zone, so the flat `8.25%` fallback
  applies to the pre-discount subtotal.
- **Shipping $5.99** — the default "Standard" method, flat below the $75 free-shipping threshold.
- **Grand total $33.50** = `28.00 − 2.80 + 2.31 + 5.99`. The **$10 gift card** tenders last, so
  only **$23.50** is charged to the card.
- **Refund $27.51** on the full return = `subtotal − discount + tax` (`28.00 − 2.80 + 2.31`);
  shipping is not refunded. It is split by tender: **$19.30 to the card** and **$8.21 back to the
  gift card** (proportional to the $23.50 / $10 split of the $33.50 total), which is why the card's
  balance ends at **$8.21**.

## Running the tests

```bash
cd server && dotnet test                            # 217 xUnit tests
cd client && pnpm test                              # 38 Vitest tests
cd client && pnpm typecheck && pnpm lint            # tsc -b + oxlint
```

If the client suite shows "Failed to start forks worker" / "Timeout waiting for worker" under a
busy machine, that is a known jsdom-under-load flake — see `docs/testing.md` for the mitigation
(`--pool=threads`, `--testTimeout=30000`, and per-file isolation).
