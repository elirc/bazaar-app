# Architecture Decision Records

Short records of the load-bearing decisions in Bazaar — the context, the choice, and what it costs.
Each reflects a decision actually embodied in the code.

| #    | Decision                                             | Status   |
| ---- | ---------------------------------------------------- | -------- |
| [0001](0001-money-as-cents.md) | Store money as integer cents | Accepted |
| [0002](0002-tender-ordering.md) | Totals order: discount → tax → shipping, gift card tendered last | Accepted |
| [0003](0003-reserve-charge-commit.md) | Checkout reserves stock, charges, then commits | Accepted |
| [0004](0004-shipment-derived-status.md) | Order fulfillment status is derived from shipment coverage | Accepted |
| [0005](0005-guids-value-generated-never.md) | Domain-assigned Guid keys (`ValueGeneratedNever`) | Accepted |
| [0006](0006-claimtypes-role.md) | Issue roles under the standard `ClaimTypes.Role` claim | Accepted |
| [0007](0007-hmac-webhooks.md) | HMAC-SHA256 signed webhooks with capped retries + delivery log | Accepted |
| [0008](0008-guest-cart-tokens.md) | Opaque cart tokens for guest checkout | Accepted |
| [0009](0009-sqlitepclraw-pin.md) | Pin `SQLitePCLRaw.bundle_e_sqlite3` to 3.0.3 | Accepted |
| [0010](0010-optimistic-concurrency.md) | Optimistic concurrency on stock and carts | Accepted |
| [0011](0011-tender-aware-refunds.md) | Tender-aware refunds (card share + gift-card restore) | Accepted |
