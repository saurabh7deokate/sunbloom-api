# ADR-0011 — Use ASP.NET Core Identity's password hasher, not the full Identity stack

**Status:** Accepted · **Date:** 2026-08-16 · **Refines** ARCHITECTURE.md §4.1

## Context

ARCHITECTURE.md §4.1 says "ASP.NET Core Identity with JWT access tokens plus refresh
tokens", which reads as adopting the full Identity stack: `UserManager`,
`SignInManager`, `IdentityDbContext`, and its schema.

That conflicts with DATABASE.md §2, which specifies `identity.users` and
`identity.refresh_tokens` with columns chosen for this domain. Full Identity brings
roughly seven tables — users, roles, user-roles, claims, logins, tokens, user-tokens —
of which SunBloom currently uses one.

The tension had to be resolved before writing the first migration, since it determines
the schema.

## Decision

Use **`PasswordHasher<T>` only.** Own the entities, persistence, and token flow.

- Password hashing uses `PasswordHasher<User>` — PBKDF2-HMAC-SHA512, 210,000
  iterations, per-password salt, with built-in rehash-on-verify.
- `User` and `RefreshToken` are plain domain entities matching DATABASE.md.
- No `UserManager`, no `SignInManager`, no `IdentityDbContext`.

**This costs zero new dependencies.** `PasswordHasher<T>` ships in the ASP.NET Core
shared framework, already referenced via `FrameworkReference Microsoft.AspNetCore.App`.
An explicit `Microsoft.Extensions.Identity.Core` package reference was added at first
and removed — NuGet flagged it as redundant (NU1510).

## Consequences

**Positive.** The one genuinely dangerous piece — password hashing — uses a vetted,
maintained implementation. "Don't roll your own crypto" is honoured where it matters.

**Positive.** The schema stays exactly as designed and documented. No unused tables, no
`AspNet*` naming leaking into a domain schema.

**Positive.** Far less framework surface for an agent-built codebase to misuse.
`UserManager` has a large API with subtle correctness requirements.

**Negative.** Features that come free with full Identity must be built when needed:
account lockout, email confirmation, password reset tokens, two-factor, external
logins. Lockout partially mitigated now by rate limiting on auth endpoints.

**Negative.** Role-based authorization is not free. When admin and content-reviewer
roles arrive (Phase 2 content review), they need modelling — likely a simple `roles`
column or table, not the full Identity role stack.

**Reversible.** Migrating to full Identity later means adopting its schema and
back-filling. Non-trivial but bounded, and unlikely to be worth doing.

## Alternatives rejected

**Full ASP.NET Core Identity.** More machinery than the product needs, an opinionated
schema that conflicts with DATABASE.md, and a large API surface. Reconsider if
SunBloom needs external logins or 2FA, where re-implementation would be genuinely
risky.

**Hand-written PBKDF2 or bcrypt wrapper.** Rejected outright. ARCHITECTURE.md §4.1 says
no custom crypto, and this is exactly the case that rule exists for.
