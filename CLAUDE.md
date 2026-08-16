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

5a. **Endpoints return typed result unions** (`Results<Ok<T>, ProblemHttpResult, …>`),
   never bare `IResult`. An `IResult` handler produces **no response schema** in the
   OpenAPI document, which silently breaks the Angular client's generated types — the
   entire mitigation for the two-repo split (ADR-0007). Regenerate the committed
   `openapi/sunbloom-api.json` whenever a contract changes.

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

.NET 10.0.302 · no Docker installed · Node v20.19.0

**Two PostgreSQL servers run on this machine. Use the right one.**

| Port | Version | Use |
|---:|---|---|
| 5432 | PostgreSQL **12.17** | ❌ Not ours. End-of-life since Nov 2024, holds unrelated databases |
| **5433** | PostgreSQL **18.4** | ✅ SunBloom |

`psql` on PATH is the 18.4 client, so `psql --version` reports 18.4 while connecting to
the *12.17* server by default. Always pass `-p 5433` explicitly.

Connection strings and JWT signing keys live in `dotnet user-secrets`, never in
`appsettings.json`.

## Getting started

```bash
# 1. Create the database and role (choose your own password)
psql -U postgres -h localhost -p 5433 \
     -v app_password="'your-password'" -f scripts/setup-database.sql

# 2. Store secrets outside the repo (one line each)
dotnet user-secrets set "ConnectionStrings:SunBloomDb" "Host=localhost;Port=5433;Database=sunbloom_dev;Username=sunbloom;Password=your-password" --project src/SunBloom.Api
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/SunBloom.Api

# 3. Verify
dotnet tool restore                            # dotnet-ef is a pinned local tool
dotnet test                                    # architecture tests must pass
dotnet run --project src/SunBloom.Api          # then GET /health/ready
```

Migrations apply automatically **in Development only**. In any other environment they
are a deliberate deployment step.

The solution file is **`SunBloom.slnx`** — the .NET 10 XML format, not `.sln`.

## Gotchas that have already cost time

- **Check the build's exit code, not grep's.** `dotnet build | grep error && dotnet run
  --no-build` runs a *stale* assembly on failure, because `&&` sees grep's status.
- **Stop the running API before building.** It locks the module DLLs and the build fails
  with MSB3027.
- **`dotnet ef migrations add` output is exempt from style rules** via `.editorconfig`
  (`[**/Migrations/*.cs]`). Without that, generated migrations fail the build on IDE0161.
- **EF migration classes are public by design**, so `ModuleBoundaryTests` exempts the
  `.Migrations` namespace.

## Current state

**Sub-slices 1.1–1.4 complete.** 31 backend tests, plus 6 unit and 4 E2E in the UI.

- 1.1 — skeleton, four modules via `IModule`, health checks, Serilog, OpenTelemetry,
  architecture tests, CI
- 1.2 — Identity: register, login, JWT, refresh rotation with family revocation on
  reuse, rate limiting, `IOwnedByUser` enforcement
- 1.3 — Catalog: global skill graph, typed edges, prerequisite cycle rejection,
  35 hand-authored .NET skills, tree and detail endpoints

Migrations and seeding run automatically on Development startup. The seeder is
idempotent by slug, so restarting is safe.

- 1.4 — Angular shell: auth flow, route guards, skill tree and detail, TypeScript types
  generated from `openapi/sunbloom-api.json`

Next: **1.5 — content generator CLI and review workflow**. See `docs/ROADMAP.md`. Target
career path for the first complete vertical is **.NET Backend Developer**, chosen because
the owner can personally judge whether the generated content is any good.

**Known gap:** `SkillGraphService.AddRelationshipAsync` is untested — it has no callers
until write endpoints arrive in 1.5, and gets an integration test then.
