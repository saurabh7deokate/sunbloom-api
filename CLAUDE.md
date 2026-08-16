# SunBloom API — working context

Read this first, every session. It is the shortest path to being useful here.

## What SunBloom is

A career and skill-development platform. It answers one question:

> **What should I do next to become better prepared for my target career?**

The loop: pick a target career → see required skills → assess where you stand →
get gaps ranked → get told what to do today → evidence accumulates → repeat.

It is **not** a course platform, notes app, quiz app, or chatbot. If a proposed
feature does not feed that loop, it does not belong yet.

## Repositories

| Repo | Contents |
|---|---|
| `sunbloom-api` (this one) | Backend, domain model, database, **all architecture docs and ADRs** |
| `sunbloom-ui` | Angular frontend |

Two repos, deliberately. The cost is contract drift; the mitigation is that
**TypeScript client types are generated from this API's OpenAPI document** — never
hand-written in the UI repo. If you change an API contract here, the UI's generated
types must be regenerated in the same working session.

## Read before designing anything

| Document | When you need it |
|---|---|
| `docs/ARCHITECTURE.md` | Module boundaries, backend structure, security, scalability |
| `docs/DOMAIN-MODEL.md` | Entities, aggregates, invariants |
| `docs/DATABASE.md` | Schema, ownership columns, versioning |
| `docs/SCORING.md` | Competency scoring, gap ranking, daily plan — **the heart of the product** |
| `docs/ROADMAP.md` | What we are building now vs. later |
| `docs/RISKS.md` | Known traps |
| `docs/decisions/` | ADRs — the *why* behind everything above |

If a decision here contradicts an ADR, the ADR wins until it is superseded by a new
one. Do not silently change a documented decision; write ADR-00NN superseding it.

## Non-negotiable rules

These exist because they are expensive or impossible to fix later.

1. **Never store a mutable skill score.** Competency is derived from an append-only
   evidence ledger through a versioned scoring function. See ADR-0002. A `Score`
   column on a user-skill row is the single most damaging thing you could add.

2. **Evidence is append-only.** No `UPDATE`, no `DELETE`. Corrections are new rows;
   retractions are tombstone rows referencing the retracted evidence.

3. **`OwnerUserId` on every personal table**, with an EF global query filter. Not
   optional, not "added later". See ADR-0003.

4. **Modules communicate only through `Contracts`.** Everything else in a module is
   `internal`. This is compiler-enforced, not a convention. See ADR-0001.

5. **No EF entity ever appears in an API contract.** Separate DTOs, always.

6. **No `DateTime.Now` / `DateTime.UtcNow` in domain or application code.** Inject
   `IClock`. Scoring involves time decay; untestable time makes it unverifiable.

7. **Approved content and career path versions are immutable.** Edits create a new
   draft version. Historical assessments must remain reproducible. See ADR-0010.

8. **No AI at runtime** through Phase 4. Content is generated offline by
   `tools/SunBloom.ContentGenerator` and human-reviewed. See ADR-0005.

9. **Git identity is personal and isolated.** This project is never connected to
   work accounts, employer CI, private feeds, or org resources. SSH remotes only.

## Working style in this repo

- **One vertical slice at a time**, sized to finish *and verify* within a single
  session. A slice spanning sessions is where architectural drift enters.
- **Run the architecture tests** (`tests/SunBloom.ArchitectureTests`) before
  considering any change done. They fail the build on boundary violations.
- Do not add a dependency without saying why in the PR/commit body.
- Do not introduce Redis, Docker, message brokers, or microservices without a
  measured need. See ADR-0008.
- Prefer deleting speculative code over keeping it "for later".

## Environment

.NET 10.0.302 · PostgreSQL 18.4 running locally (no Docker installed) · Node v20.19.0

Local Postgres is the dev database. Connection strings and JWT signing keys live in
`dotnet user-secrets`, never in `appsettings.json`.

## Current state

Phase 1, slice 1 — see `docs/ROADMAP.md`. Target career path for the first complete
vertical is **.NET Backend Developer**, chosen because the owner can personally judge
whether the generated content is any good.
