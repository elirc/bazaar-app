# 0005 — Domain-assigned Guid keys (`ValueGeneratedNever`)

**Status:** Accepted

## Context

Aggregates create their identity in the domain (`Guid.NewGuid()` in entity initializers) so the
domain layer stays free of the persistence layer. EF Core, however, defaults to treating a `Guid`
key as store-generated, and uses a default/empty key to decide whether a tracked entity is new.

## Decision

Configure **every `Guid` primary key as `ValueGeneratedNever`** (done in a loop over the model in
`OnModelCreating`), so EF treats the domain-assigned value as authoritative.

## Consequences

- A new child added to an already-tracked aggregate (e.g. a cart line on an existing cart) is
  correctly `INSERT`ed rather than mistaken for an existing row and `UPDATE`d.
- Identity is available immediately on construction, before saving — useful for wiring up
  owned/child relationships in memory.
- Composite owned collections (`VariantOption`, `TaxCategoryRate`) use explicit composite keys for
  the same "no surprise shadow key" reason.
