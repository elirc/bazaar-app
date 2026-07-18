# 0009 — Pin `SQLitePCLRaw.bundle_e_sqlite3` to 3.0.3

**Status:** Accepted

## Context

`Microsoft.EntityFrameworkCore.Sqlite` brings the SQLite native bundle transitively through
`SQLitePCLRaw`. An older transitive version is flagged by advisory **`GHSA-2m69-gcr7-jv3q`**, so a
`dotnet` NuGet audit reports a vulnerability.

## Decision

Add a direct, explicit `PackageReference` to **`SQLitePCLRaw.bundle_e_sqlite3` version `3.0.3`** in
`Bazaar.Infrastructure`, pinning the native bundle to a release that resolves the advisory. No audit
suppression is used.

## Consequences

- The NuGet audit is clean with no suppression entry to maintain.
- The pin must be revisited when EF Core's transitive dependency advances past 3.0.3 (keep the pin
  ≥ the fixed version). A `Directory.Build.props` comment documents the rationale in-tree.
