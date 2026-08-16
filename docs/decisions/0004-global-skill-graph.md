# ADR-0004 — One global skill graph, shared by all career paths

**Status:** Accepted · **Date:** 2026-08-16

## Context

Career paths require skills. The intuitive model gives each path its own skill tree —
a ".NET Developer" tree, a "Java Developer" tree — matching how §7 presents the example.

Content is AI-generated (ADR-0005). Generating skills per path means an LLM produces the
same concept under different names in different trees: "LINQ", "LINQ Queries", and
"Language Integrated Query"; "REST APIs" and "RESTful Web Services".

Every cross-path capability then breaks silently:

- **Career switching** — .NET → Java cannot tell which skills transfer.
- **Job matching** (Phase 6) — a job description maps to one path's vocabulary only.
- **Multiple goals** — the same skill is scored twice under two identities.

Silently is the important word. Nothing errors; the comparisons are simply meaningless.

## Decision

**Skills are global and canonical.** One graph, shared by every career path.

- `catalog.skills` has a unique `slug` — the canonical identity (`csharp-linq`).
- A career path *version* references skills through `career_skill_requirements`, with a
  required level and importance per dimension.
- A career path never owns or defines a skill.
- The content generator resolves against existing skills before creating new ones;
  near-duplicates are a review-time rejection.

## Consequences

**Positive.** Cross-path comparison is meaningful by construction. Evidence accumulates
against one identity, so learning C# for one goal counts toward every goal requiring it —
which is exactly right, and would be false under per-path trees. Phase 6 job matching
has a vocabulary to map onto.

**Negative.** Generation is harder: the generator must reconcile against an existing
graph rather than emitting a fresh tree. Accepted — this cost is paid once per path,
whereas duplicate-skill damage compounds forever.

**Negative.** Skill naming becomes a shared concern; a badly-named skill affects every
path. Mitigated by review.

## Alternatives rejected

**Per-path skill trees.** Simpler generation, but breaks every cross-path feature the
product is eventually for.

**Per-path trees plus a mapping table.** All the complexity of canonical skills, plus a
mapping table that will be incomplete, plus ambiguity about which side is authoritative.
