# Architecture Decision Records

Each ADR records **why** a decision was made, so it is not silently reversed later by
someone — human or agent — who lacks the context that produced it.

## Rules

- ADRs are **immutable once accepted.** To change a decision, write a new ADR that
  supersedes the old one and update the old one's status. Never edit the reasoning.
- If code contradicts an ADR, the code is wrong until a superseding ADR exists.
- Every ADR states the alternatives that were rejected, and why. The rejected options
  are usually the more useful half — they are what stops the same debate recurring.

## Index

| # | Decision | Why it matters |
|---|---|---|
| [0001](0001-modular-monolith-with-enforced-boundaries.md) | Modular monolith with compiler-enforced boundaries | Boundaries survive an agent developer with no session memory |
| [0002](0002-evidence-ledger-not-stored-scores.md) | Competency derived from an append-only evidence ledger | **The load-bearing decision.** Do not reverse |
| [0003](0003-user-owned-data-not-multitenancy.md) | User-owned data with explicit grants | The one genuinely expensive retrofit, done up front |
| [0004](0004-global-skill-graph.md) | One global skill graph shared by all career paths | Without it, every cross-path feature breaks silently |
| [0005](0005-offline-content-generation-no-runtime-ai.md) | Content generated offline, human-reviewed | Removes AI from the runtime entirely until Phase 5 |
| [0006](0006-schema-per-module-with-documented-cross-schema-fks.md) | Schema per module; cross-schema FKs permitted | Integrity chosen over extraction purity, deliberately |
| [0007](0007-two-repositories-with-generated-client-types.md) | Two repos, generated TypeScript types | Manages the drift cost of the repo split |
| [0008](0008-defer-redis-docker-external-idp.md) | Defer Redis, Docker, brokers, external IdP | Each deferral has a named adoption trigger |
| [0009](0009-angular-signals-not-ngrx.md) | Angular signals, not NgRx | Less ceremony; SignalStore is the escalation path |
| [0010](0010-immutable-approved-career-path-versions.md) | Approved career path versions are immutable | Makes historical assessments reproducible |
| [0011](0011-password-hashing-without-full-aspnet-identity.md) | `PasswordHasher<T>` only, not the full Identity stack | Vetted hashing, our own schema, zero new dependencies |

## The three that carry the most weight

**0002** is the one that must never be reversed. A mutable score column would destroy
explainability, versioned scoring, and reproducible history in a single commit.

**0003** is the expensive-to-retrofit one. Ownership columns added later reliably miss a
table, and the missed table is the one that leaks.

**0001** is what keeps the other two enforced across many independent sessions.
