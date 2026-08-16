# ADR-0006 — A DbContext and schema per module, with documented cross-schema FKs

**Status:** Accepted · **Date:** 2026-08-16

## Context

Modules need data isolation matching their code isolation (ADR-0001). Two questions
follow: one `DbContext` or several, and whether foreign keys may cross module schemas.

Strict modular-monolith practice says separate contexts and **no** cross-schema FKs, so
modules can be extracted independently.

## Decision

**One `DbContext` and one Postgres schema per module**, in a single database.

`identity` · `catalog` · `competency` · `shared`

**Cross-schema foreign keys are permitted**, and every one is enumerated in
DATABASE.md §6 with its unwind path.

## Consequences

### On separate contexts

**Positive.** EF cannot express a navigation property across contexts, so a cross-module
join is not merely discouraged — it is impossible. That matches ADR-0001's principle of
compiler-enforced rather than convention-enforced boundaries, which matters more than
usual with an agent developer that does not remember conventions.

**Negative.** Multiple migration histories; more commands, more to get wrong. Bounded,
and wrapped in a script.

### On permitting cross-schema FKs

This rejects the purist position deliberately, and the reasoning should stay visible:

- Orphaned evidence pointing at a deleted skill would **silently corrupt every score
  derived from it**, and would be nearly undetectable after the fact.
- The probability of extracting a module within two years is low.

Expected value favours integrity over extraction convenience. Catalog content is
soft-deleted rather than hard-deleted, so these FKs are rarely exercised anyway.

**Negative.** Extraction requires dropping the listed FKs and adding application-level
validation. Bounded and documented — a known task, not an archaeological dig.

## Alternatives rejected

**Single `DbContext` for everything.** Simplest migrations, but permits cross-module
navigation properties. The boundary would then rest on discipline, which ADR-0001 rejects.

**Separate databases per module.** Real isolation, but loses transactional consistency and
demands infrastructure that does not exist for a product with no users.

**No cross-schema FKs.** Architecturally pure, but trades a certain and severe risk (silent
score corruption) for an unlikely convenience (frictionless extraction).
