# 0001 — Store money as integer cents

**Status:** Accepted

## Context

Amounts must round-trip exactly and sum without drift. SQLite has no native decimal type; it stores
numbers as `REAL` (binary floating point) or `TEXT`. Persisting `decimal` as either invites
rounding drift and makes cent-level equality assertions unreliable.

## Decision

Model money as an immutable `Money` value object (`decimal Amount` + 3-letter `Currency`) that
rounds to two places with banker's rounding on construction, and **persist it as integer minor
units (cents, a `long`)** via `DecimalToCentsConverter`. Each owned `Money` navigation maps to
`{Prefix}Amount` (cents) and `{Prefix}Currency` columns.

## Consequences

- Exact storage and comparison; `ToCents()`/`FromCents()` round-trip losslessly.
- Arithmetic goes through `Money` (add/subtract/multiply/rate) with consistent rounding.
- A sharp edge: EF owned-type tracking keys on the object instance, so the same `Money` instance
  must never be assigned to two owned navigations — see ADR 0005's cousin note and the
  no-shared-instance rule in `architecture.md`. Always construct fresh instances.
