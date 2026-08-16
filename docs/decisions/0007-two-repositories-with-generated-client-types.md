# ADR-0007 — Two repositories, with TypeScript types generated from OpenAPI

**Status:** Accepted · **Date:** 2026-08-16

## Context

The owner chose separate `sunbloom-api` and `sunbloom-ui` repositories. A monorepo was
recommended — one clone, atomic commits across API and UI, and the whole system visible
in a single agent session — and the separate-repo option was chosen with those trade-offs
stated.

The decision is settled. This ADR records the consequence that must be managed.

## Decision

Two repositories. **The structural cost — contract drift — is mitigated by generating
the UI's TypeScript API types from the backend's OpenAPI document.**

- The OpenAPI document is generated from the API and **committed** to `sunbloom-api`.
- `sunbloom-ui` generates types from it into `src/app/api/generated/`.
- Generated types are **never hand-edited**, and never worked around with `any`.
- A contract change and its regeneration happen in the **same working session**.
- Architecture docs and ADRs live in `sunbloom-api`; each repo has its own `CLAUDE.md`.

## Consequences

**Positive.** Contract mismatches surface as TypeScript compile errors rather than
runtime failures. Independent deployment and cleaner per-repo history.

**Negative.** No atomic cross-stack commit. A breaking API change is two commits in two
repos and can land half-applied.

**Negative.** An agent session sees only one side of the system. This is why each repo's
`CLAUDE.md` points explicitly at the other.

**Residual risk.** Nothing enforces regeneration automatically. Tracked as RISKS.md R6;
the escalation, if drift actually occurs, is a CI check comparing the committed OpenAPI
document against the UI's generated types.

## Alternatives rejected

**Monorepo.** Recommended and not chosen. Would have removed drift risk entirely.

**Hand-written TypeScript interfaces.** Reintroduces exactly the drift the split makes
dangerous, with no compile-time signal when they diverge.

**Shared contracts package.** A third repo to version and publish, for one consumer.
