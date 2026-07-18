# API reference

Base URL in development: `http://localhost:5180`. All request and response bodies are JSON.

## Conventions

- **Auth.** Send `Authorization: Bearer <token>` (obtained from `/api/auth/login` or
  `/api/auth/register`). Endpoints are one of: **public**, **authenticated** (any signed-in
  customer), or **admin** (`RequireRole("Admin")`).
- **Errors** are RFC 7807 ProblemDetails (`application/problem+json`) with `status`, `title`, and
  usually `detail`. Validation failures return `400` with a `errors` map (field → messages).
- **Money** is `{ "amount": <decimal>, "currency": "USD" }`.
- **Paging.** List endpoints accept `page` (default 1) and `pageSize` (clamped to a per-area max:
  storefront 60, admin 100) and return `{ items, page, pageSize, totalCount, totalPages,
  hasPrevious, hasNext }`.
- **Common status codes.** `401` unauthenticated on a protected route; `403` authenticated but not
  admin; `404` not found (also used for cross-account access, to avoid existence leaks); `409`
  conflicts (stale concurrency, invalid transition, over-refund/over-ship, duplicate slug/SKU/code);
  `429` checkout rate limit; `402` payment/refund declined.

## Auth — `/api/auth`

| Method | Route            | Auth   | Body                                   | Success | Errors |
| ------ | ---------------- | ------ | -------------------------------------- | ------- | ------ |
| POST   | `/register`      | public | `{ email, password, firstName?, lastName? }` | `201` `AuthResponse` | `400` (password < 8 chars, bad email), `409` email already registered |
| POST   | `/login`         | public | `{ email, password }`                  | `200` `AuthResponse` | `400`, `401` invalid credentials |
| GET    | `/me`            | auth   | —                                      | `200` `CustomerDto` | `401` |

`AuthResponse = { token, expiresAt, customer: CustomerDto }`;
`CustomerDto = { id, email, firstName, lastName, role }` where `role` is `Customer` or `Admin`.

## Storefront — `/api/storefront`

| Method | Route | Auth | Notes |
| ------ | ----- | ---- | ----- |
| GET | `/products` | public | Query: `search`, `collection` (slug), `sort` (`price_asc`\|`price_desc`\|`title_asc`\|`title_desc`; default newest), `page`, `pageSize`. Returns `PagedResult<ProductSummaryDto>` (active products only, with aggregate rating). |
| GET | `/products/{slug}` | public | `200` `ProductDetailDto` (images, variants with availability + options, collections, rating) or `404`. |
| GET | `/collections` | public | `200` `CollectionDto[]` with active product counts. |
| GET | `/discounts/{code}` | public | Query: `subtotal?`, `currency?`. `200` `DiscountPreviewDto { code, valid, reason?, discount? }` — invalid/unknown codes return `valid: false` (never an error). |
| GET | `/products/{slug}/reviews` | public | `200` `ReviewDto[]` — **approved** reviews only, sorted by helpfulness. |
| POST | `/products/{slug}/reviews` | auth | Body `{ rating (1–5), title?, body }`. **Verified-purchase gated**: `403` if the customer has no paid/fulfilled order containing the product; `409` if already reviewed; `201` `ReviewDto` (status `Pending` until moderated). |
| POST | `/reviews/{id}/helpful` | auth | One helpful vote per customer. `200 { helpfulCount }`, `409` if already voted, `404` if not an approved review. |
| GET | `/gift-cards/{code}` | public | `200` `GiftCardBalanceDto { code, valid, balance? }` — unknown/empty cards return `valid: false`. |

## Cart — `/api/cart`

Carts are identified by an opaque `token`. Guests and signed-in customers both use these routes.

| Method | Route | Auth | Notes |
| ------ | ----- | ---- | ----- |
| POST | `/` | optional | Create a cart (tagged with the customer if signed in). `201` `CartDto`. |
| GET | `/{token}` | optional | `200` `CartDto` or `404`. |
| POST | `/{token}/items` | optional | Body `{ variantId, quantity (1–99) }`. Merges with an existing line. `200` `CartDto`; `400` invalid quantity / over per-line max (99); `404` cart or variant not available. |
| PUT | `/{token}/items/{variantId}` | optional | Body `{ quantity (0–99) }` (0 removes the line). `200`/`404`. |
| POST | `/{token}/items/{variantId}/saved` | optional | Body `{ saved: bool }` — toggle save-for-later (excluded from totals & checkout). `200`/`404`. |
| DELETE | `/{token}/items/{variantId}` | optional | Idempotent remove. `200` `CartDto`. |

## Checkout & orders

| Method | Route | Auth | Notes |
| ------ | ----- | ---- | ----- |
| GET | `/api/checkout/shipping-options` | public | Query: `cartToken`. `200` `ShippingOption[]` priced for the cart; `400` missing token; `404` cart. |
| POST | `/api/checkout` | optional, **rate-limited** | Body `CheckoutRequest`. `201` `OrderDto`. See below. |
| GET | `/api/orders/{id}` | public | `200` `OrderDto` (with shipments) or `404`. Public order lookup by id. |

`CheckoutRequest = { cartToken, email, shippingAddress: { name, line1, line2?, city, region?,
postalCode, country (2-letter) }, discountCode?, shippingMethodCode?, giftCardCode? }`.

Checkout error mapping: `400` validation / invalid discount / invalid shipping method / invalid
gift card; `404` cart not found; `409` cart empty / insufficient stock; `402` payment declined;
`429` too many checkout attempts (fixed-window rate limit).

## Account — `/api/account` (all authenticated)

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/orders` | `200` `OrderSummaryDto[]` — the caller's orders only. |
| GET | `/orders/{id}` | `200` `OrderDto` or `404` (including another customer's order). |
| GET | `/addresses` | `200` `AddressBookDto[]` (default first). |
| POST | `/addresses` | Body `{ label?, isDefault?, address }`. `201`. First address becomes the default. |
| PUT | `/addresses/{id}` | `200`/`404`. |
| DELETE | `/addresses/{id}` | `204`/`404`. |
| GET | `/returns` | `200` `ReturnRequestDto[]` — the caller's returns. |
| POST | `/orders/{orderId}/returns` | Body `{ reason?, lines: [{ orderLineItemId, quantity }] }`. `201` `ReturnRequestDto`; `404` not the caller's order; `409` order not fulfilled / over-refund; `400` invalid line. |
| GET | `/wishlists` | `200` `WishlistDto[]` (creates a default wishlist on first read). |
| POST | `/wishlists` | Body `{ name }`. `201`. |
| DELETE | `/wishlists/{id}` | `204`; `400` cannot delete the default; `404`. |
| POST | `/wishlist/items` | Body `{ variantId }` — add to the default wishlist. |
| POST | `/wishlists/{id}/items` | Body `{ variantId }`. `200`/`404`. |
| DELETE | `/wishlists/{id}/items/{variantId}` | `200`/`404`. |
| POST | `/wishlists/{id}/items/{variantId}/move-to-cart` | Body `{ cartToken? }` — move an item into a cart (creates one if omitted). `200` `CartDto`; `404`; `409` variant unavailable. |

## Admin — `/api/admin` (all require the `Admin` role)

### Products, variants, collections

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/products` | Query `search`, `status`, `page`, `pageSize`. `PagedResult<ProductSummaryDto>`. |
| GET | `/products/{id}` | `200` `ProductDetailDto` / `404`. |
| POST | `/products` | Body `CreateProductRequest` (title, slug, status?, taxCategory?, images[], variants[] with sku/price/stock, collectionSlugs[]). `201`; `400` validation / unknown status / bad collection slug; `409` duplicate slug or SKU. |
| PUT | `/products/{id}` | `200`/`404`/`409`. |
| DELETE | `/products/{id}` | `204`; `409` if referenced by carts/orders. |
| PUT | `/variants/{id}` | Body `{ title?, price, currency?, stockOnHand?, weightGrams? }`. `200`/`404`. |
| GET | `/collections` | `200` `CollectionDto[]`. |
| POST | `/collections` | Body `{ title, slug, description? }`. `201`/`409` duplicate slug. |
| PUT | `/collections/{id}` | `200`/`404`/`409`. |
| DELETE | `/collections/{id}` | `204`/`404`. |

### Orders & fulfillment

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/orders` | Query `search`, `status`, `page`, `pageSize`. `PagedResult<OrderSummaryDto>`. |
| GET | `/orders/{id}` | `200` `OrderDto` (with shipments) / `404`. |
| POST | `/orders/{id}/transition` | Body `{ status }`. `200` `OrderDto`; `400` unknown status; `409` illegal transition (`title: "Invalid transition"`); `404`. Restocks on `Cancelled`/`Refunded`; fires `order.refunded` webhook on refund. |
| POST | `/orders/{id}/shipments` | Body `{ carrier, trackingNumber, lines: [{ orderLineItemId, quantity }] }`. `201` `OrderDto` (status derived from coverage); `409` not shippable / over-shipment; `404`. Fires `order.fulfilled` when the shipment completes coverage. |

### Discounts, reviews, returns, gift cards

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/discounts` | `200` `DiscountDto[]`. |
| POST | `/discounts` | Body `{ code, type (Percentage\|FixedAmount), value, currency?, isActive?, startsAt?, endsAt?, usageLimit? }`. `201`; `400` bad type / percentage > 100; `409` duplicate code. |
| DELETE | `/discounts/{id}` | `204`/`404`. |
| GET | `/reviews` | Query `status`, `page`, `pageSize`. `PagedResult<AdminReviewDto>`. |
| POST | `/reviews/{id}/moderate` | Body `{ status (Approved\|Rejected\|Pending) }`. `200 { id, status }`; `400` unknown status; `404`. |
| GET | `/returns` | Query `status`, `page`, `pageSize`. `PagedResult<AdminReturnDto>`. |
| POST | `/returns/{id}/approve` | Issues the tender-aware refund + restock. `200` `AdminReturnDto`; `409` already decided; `402` refund failed; `404`. |
| POST | `/returns/{id}/reject` | Body `{ reason? }`. `200`; `409` already decided; `404`. |
| GET | `/gift-cards` | `200` `GiftCardDto[]`. |
| POST | `/gift-cards` | Body `{ amount, code?, currency? }`. `201` `GiftCardDto` (generates a code if omitted); `409` duplicate code. |

### Reports & webhooks

| Method | Route | Notes |
| ------ | ----- | ----- |
| GET | `/reports/sales` | `200` `SalesReportDto { buckets[], totalOrders, totalRevenue }` (sold statuses only). |
| GET | `/reports/top-products` | Query `limit?`. `200` `TopProductDto[]`. |
| GET | `/reports/low-stock` | Query `threshold?` (default 10). `200` `LowStockDto[]`. |
| GET | `/reports/discounts` | `200` `DiscountUsageDto[]`. |
| GET | `/webhooks` | `200` `WebhookSubscriptionDto[]`. |
| POST | `/webhooks` | Body `{ url, events[], secret? }`. `201` (generates a secret if omitted); `400` unknown event(s). Valid events: `order.created`, `order.paid`, `order.fulfilled`, `order.refunded`. |
| DELETE | `/webhooks/{id}` | `204`/`404`. |
| GET | `/webhooks/deliveries` | Query `subscriptionId?`. `200` `WebhookDeliveryDto[]` (last 100). |

## Ops

| Method | Route | Auth | Notes |
| ------ | ----- | ---- | ----- |
| GET | `/health` | public | `200` `{ status: "ok", service: "bazaar-api", checks: { database: "ok" }, timestamp }` with a live DB probe; `503` with `status: "degraded"` if the database is unreachable. |
| GET | `/` | public | `"Bazaar API"` liveness string. |
