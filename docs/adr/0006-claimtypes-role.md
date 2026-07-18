# 0006 — Issue roles under the standard `ClaimTypes.Role` claim

**Status:** Accepted

## Context

Admin endpoints are protected with `RequireRole("Admin")`. ASP.NET Core's role checks
(`IsInRole`, `RequireRole`) compare against the identity's **`RoleClaimType`**, which defaults to
`ClaimTypes.Role` (the URI `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`). A token
that puts the role under an arbitrary key that isn't mapped to that role-claim type would authenticate
but never satisfy the policy.

## Decision

`JwtTokenService` writes the role using the **standard `ClaimTypes.Role`** claim key, and the API
keeps the default `RoleClaimType`. `NameClaimType` is set to `sub`. The admin policy is
`RequireAuthenticatedUser().RequireRole("Admin")`.

## Consequences

- Role checks work without custom claim-type remapping in the authorization layer.
- Authentication is not authorization: a valid token whose role is not `Admin` (or that carries no
  role) is **403**; a guest is **401**. This is covered by `AuthorizationMatrixTests`.
- Because JwtBearer's default `MapInboundClaims` maps the short `role` name onto `ClaimTypes.Role`,
  both forms happen to resolve; standardizing on `ClaimTypes.Role` at issuance keeps the contract
  explicit and independent of that mapping.
