# ADR-0002 — Competency derived from an append-only evidence ledger

**Status:** Accepted · **Date:** 2026-08-16 · **Supersedes nothing. Do not reverse.**

## Context

Every valuable feature in SunBloom consumes one judgement: how good is user *U* at
skill *S*? Gap analysis, recommendations, readiness, job matching, and resume
verification all read it.

The obvious implementation is a `user_skills` table with a `score` column updated as
new information arrives. The spec also asks for four separately-tracked dimensions
(§8), a versioned scoring system (§8), reproducible historical assessments (§24), and
an answer to *"why does SunBloom think I am strong in this?"* (§19).

A mutable score column cannot satisfy any of those. It destroys the input on every
write, so the reasoning behind a number is gone the moment it is computed.

## Decision

**No score is ever stored.** Competency is derived on read from an append-only ledger
of evidence, through an explicitly versioned scoring function.

- `skill_evidence` is append-only — no `UPDATE`, no `DELETE`, enforced by Postgres
  rules, not by convention.
- Corrections are new rows; retractions are tombstone rows.
- Scoring constants live in configuration and carry a version identifier.
- Every `Assessment` snapshots the algorithm version and career path version that
  produced it.

## Consequences

**Positive.** Four requirements fall out for free rather than being built:

| Requirement | Satisfied by |
|---|---|
| §19 explainability | The evidence *is* the explanation — a query, not a feature |
| §8 versioned scoring | Change the function and recompute; inputs are untouched |
| §24 reproducible history | Immutable evidence makes any past score re-derivable |
| Being wrong about scoring | A recomputation, not a data migration |

That last one matters most. Every constant in SCORING.md is a guess. This design makes
guessing wrong cheap.

**Negative.** Reads are more expensive than a column lookup. Accepted: the volume per
user is small, and a precomputed read model can be added on `EvidenceRecorded` if
measured — without changing the source of truth.

**Negative.** Storage grows monotonically. Accepted: rows are small, and partitioning
by `occurred_at` is available and unblocked.

## Alternatives rejected

**Mutable score column.** Cannot explain itself, cannot be re-derived, and turns any
scoring change into an irreversible migration.

**Score column plus separate audit log.** Two sources of truth that will diverge, and
the audit log inevitably becomes write-only.

**Full event sourcing across all entities.** Right for competency because the domain is
genuinely append-only; wrong as a system-wide persistence pattern. Career paths and
users are ordinary mutable entities.
