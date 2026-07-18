# 0008 — Opaque cart tokens for guest checkout

**Status:** Accepted

## Context

Shoppers must be able to build a cart and check out **without** an account, while signed-in
shoppers should have their orders attached to their account.

## Decision

A `Cart` is identified by an opaque **`Token`** (a `Guid` "N" string), unique and indexed. Cart
endpoints operate by token and require no authentication. When a cart is created by a signed-in
customer it is tagged with their `CustomerId`; at checkout the resulting order is attached to that
customer (or left account-less for a guest).

## Consequences

- Guests can shop and check out; the client persists the token in local storage.
- The token is a bearer capability for that cart — anyone with it can read/modify the cart, which is
  acceptable for a low-sensitivity, pre-purchase resource.
- Account-scoped resources (orders, addresses, wishlists, returns) still require authentication and
  are isolated per customer, returning `404` on cross-account access (no existence leak).
