# 0002 — Totals order and gift card tendered last

**Status:** Accepted

## Context

An order combines a subtotal, a discount, tax, shipping, and possibly a gift card. The order these
apply in changes the amounts, so it must be fixed and explicit.

## Decision

Compute totals in a fixed order and treat the gift card as a **tender**, not a total component:

1. `subtotal` = Σ line `price × qty`.
2. `tax` on each line's **pre-discount** amount (by tax zone; flat fallback).
3. `shipping` by the chosen (or default) method.
4. `discount` (percentage or fixed), capped at the subtotal.
5. `grandTotal = subtotal − discount + tax + shipping`.
6. The **gift card tenders last**, covering `min(balance, grandTotal)`; the card is charged
   `grandTotal − giftCardApplied`.

## Consequences

- Tax is charged on the pre-discount amount (a deliberate, documented choice).
- A gift card that covers the whole total skips the payment gateway entirely.
- The stored order records `Subtotal`, `DiscountTotal`, `TaxTotal`, `ShippingTotal`, `GrandTotal`,
  and `GiftCardTotal` separately, and cents are conserved
  (`GrandTotal = Subtotal − Discount + Tax + Shipping`).
- Refunds must respect the same tender split — see ADR 0011.
