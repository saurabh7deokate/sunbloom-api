# ADR-0010 — Career path versions are immutable once approved

**Status:** Accepted · **Date:** 2026-08-16

## Context

Career requirements change year over year (§24). What a mid-level .NET developer needed
in 2026 differs from 2028.

The spec requires that historical assessments remain reproducible against the version
they were assessed against, and that historical versions are not overwritten.

If requirements were mutable, a user's "67% ready" from six months ago would silently
become a different number — or become unexplainable, because the requirements that
produced it no longer exist.

## Decision

Identity and content are separated, and approved content is frozen.

```
career_paths            stable identity — ".NET Backend Developer"
career_path_versions    versioned content, with status Draft|Approved|Archived
career_skill_requirements   belongs to a version, never to a path
```

- Once a version is `Approved`, it and its requirements are **immutable**, enforced by a
  **database trigger** rather than by application discipline.
- Changes create a new `Draft` version.
- `user_career_goals` and `assessments` reference a **version**, never a path.
- Every `Assessment` also snapshots the `scoring_algorithm_version`.

## Consequences

**Positive.** Historical assessments are exactly reproducible: immutable requirements
(this ADR) plus immutable evidence (ADR-0002) plus a recorded algorithm version means any
past score can be re-derived and explained.

**Positive.** Progress over time is meaningful, because the yardstick did not move.

**Positive.** Enforcing immutability in the database defends the guarantee at the lowest
level, where no future code path can bypass it.

**Negative.** Editing an approved version requires creating a new one — deliberate
friction, and correct.

**Negative.** Migrating a user to a newer version of their target path is unmodelled:
their progress was measured against different requirements. Real, unsolved, and tracked
on the RISKS.md watch list. It becomes concrete in Phase 2.

## Alternatives rejected

**Mutable career paths.** Simplest, and destroys reproducibility — the one thing §24
explicitly requires.

**Full audit history on a mutable table.** Reconstructing "what did this look like in
March" from an audit trail is possible but error-prone, and every consumer would have to
do it correctly. Explicit versions make the correct thing the easy thing.

**Copy requirements onto each assessment.** Removes the shared vocabulary, duplicates
data per user, and makes "how many people target this version" unanswerable.
