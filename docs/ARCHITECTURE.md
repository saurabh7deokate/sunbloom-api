# SunBloom — Architecture

**Status:** Design, pre-implementation · **Date:** 2026-08-16

Scope of this document: system-wide boundaries and principles, with detailed design
for the first vertical slice only. Phases 5–7 are named so that later work has
somewhere to land, but are deliberately *not* designed — designing them now would be
speculation against requirements that do not exist yet.

---

## 1. Product architecture

### 1.1 The one thing that matters

Every valuable feature in SunBloom is downstream of a single question:

> How confident are we that user *U* is at level *L* in skill *S*?

Gap analysis, recommendations, readiness scoring, job matching, and resume
claim-verification are all consumers of that one answer. If it is arbitrary, SunBloom
is a dashboard of invented numbers with a career-guidance theme.

Therefore the **Competency** module is the architectural centre of the system, and it
is designed first and most carefully. Everything else is either a producer of evidence
into it or a consumer of judgements out of it.

```
     PRODUCERS OF EVIDENCE              CONSUMERS OF JUDGEMENT
     ─────────────────────              ──────────────────────
     Self-assessment  ─┐             ┌─►  Gap analysis
     Quizzes          ─┤             ├─►  Recommendations / daily plan
     Practice         ─┼──► COMPETENCY ──┼─►  Career readiness
     Interviews       ─┤   (evidence  ├─►  Job match          (Phase 6)
     Projects         ─┤    ledger)   ├─►  Resume verification (Phase 6)
     Certifications   ─┘             └─►  Revision scheduling  (Phase 3)
```

Read this diagram as the dependency rule: **evidence flows in, judgements flow out,
and nothing writes a score directly.**

### 1.2 Domains and responsibilities

| Domain | Owns | Phase |
|---|---|---|
| **Identity** | Accounts, authentication, tokens, user profile | 1 |
| **Catalog** | Career paths, versions, skill graph, topics, resources, content review | 2 |
| **Competency** | Evidence ledger, scoring, assessments, readiness | 2 |
| **Recommendations** | Gap ranking, prerequisite sequencing, daily plan | 4 (partial in 2) |
| **Learning** | Topic progress, revision scheduling | 3 |
| **Practice** | Questions, attempts, results | 3 |
| **Interviews** | Question banks, sessions, performance | 5 |
| **AI** | Provider-abstracted generation and evaluation | 5 |
| **Jobs** | Job descriptions, extraction, matching | 6 |
| **Resumes** | Parsing, claim-vs-evidence analysis | 6 |
| **Organizations** | Groups, membership, sharing grants | 7 |
| **Analytics / Notifications** | Product events, delivery | Cross-cutting, later |

The spec listed fifteen modules. Only **four** exist in code before Phase 3:
Identity, Catalog, Competency, Recommendations. A module is created when a boundary
is *felt*, not when it is anticipated — see ADR-0001.

---

## 2. Backend architecture

### 2.1 Shape

A **modular monolith**, deployed as one process, with module boundaries enforced by
the compiler rather than by discipline.

The enforcement mechanism matters more than usual here, because this project is built
largely by an AI agent that does not remember previous sessions. A folder convention
is a boundary that survives exactly as long as someone remembers it. A project
reference and an `internal` access modifier are boundaries the compiler defends.

```
SunBloom.sln
├── src/
│   ├── SunBloom.Api/                    Host. Composition root. No business logic.
│   ├── SunBloom.SharedKernel/           Result, strongly-typed IDs, IClock,
│   │                                    domain-event base, pagination primitives.
│   └── Modules/
│       ├── SunBloom.Modules.Identity/
│       ├── SunBloom.Modules.Catalog/
│       ├── SunBloom.Modules.Competency/
│       └── SunBloom.Modules.Recommendations/
├── tools/
│   └── SunBloom.ContentGenerator/       Offline CLI. Not referenced by the host.
├── tests/
│   ├── SunBloom.ArchitectureTests/      Fails the build on boundary violations.
│   ├── SunBloom.Modules.Catalog.Tests/
│   ├── SunBloom.Modules.Competency.Tests/
│   └── SunBloom.Api.IntegrationTests/
└── docs/
```

### 2.2 Inside a module

Layers are **folders within one project**, not separate projects. Four modules × three
layers as separate assemblies is twelve projects of ceremony for a codebase this size.
Layering is enforced by architecture tests instead.

```
SunBloom.Modules.Catalog/
├── Contracts/          PUBLIC. The module's entire API surface.
│                       Query interfaces, DTOs, integration events.
├── Domain/             internal. Entities, value objects, invariants.
│                       No EF, no ASP.NET, no I/O.
├── Application/        internal. Use-case handlers, orchestration.
├── Infrastructure/     internal. EF configuration, DbContext, repositories.
└── Endpoints/          internal. Minimal API mapping, registered via IModule.
```

**Everything outside `Contracts/` is `internal`.** Other modules physically cannot
reference module internals — this is not a lint rule, it is the C# access system.
Test projects get access via `InternalsVisibleTo`.

### 2.3 How modules talk

Cross-module reads go through contract interfaces, never through another module's
database or entities:

```
Recommendations ──► Catalog.Contracts.ISkillCatalogQueries
                └─► Competency.Contracts.ICompetencyQueries
```

Each module implements its own contract interfaces internally and registers them in
DI. `Recommendations` depends on the *interface*, so `Catalog` could later become an
HTTP call without `Recommendations` changing.

Cross-module *writes* go through domain events, never direct calls — see §2.5.

### 2.4 The host

`SunBloom.Api` contains no business logic. Each module exposes an `IModule`
implementation with `AddServices(...)` and `MapEndpoints(...)`; the host discovers and
calls them. This keeps endpoints internal to their module while leaving the host as a
thin composition root.

### 2.5 Events

An **in-process** event dispatcher. No broker, no queue, no Kafka. Events published
inside a transaction are dispatched after commit via an outbox table, so a failed
dispatch cannot silently lose work.

Phase 2 events:

```
SelfAssessmentSubmitted   →  Competency records evidence
EvidenceRecorded          →  Competency invalidates cached scores
CareerPathSelected        →  Recommendations warms the user's gap view
SkillLevelChanged         →  Recommendations re-ranks; Learning schedules revision (Ph3)
```

The outbox exists from the start because retrofitting delivery guarantees onto an
event system that already has consumers is significantly harder than starting with one.

---

## 3. AI architecture

### 3.1 The important consequence of the content decision

Content is **AI-generated offline and human-reviewed**, not generated per request.
Therefore **there is no AI in the runtime at all through Phase 4.** No provider SDK
in the host, no API keys in the API, no latency or cost on the request path, no
failure mode where a user's dashboard depends on a third-party service.

AI lives in `tools/SunBloom.ContentGenerator` — a console application that writes
draft content into the database for review. If it breaks, the product does not.

This is the single biggest simplification available to this project, and it comes
free from a product decision rather than an architectural one.

### 3.2 When AI does enter the runtime (Phase 5)

The spec proposed a seven-method `IAIService` (`GenerateExplanation`,
`EvaluateAnswer`, `AnalyzeResume`, …). That is a god interface: it changes shape
every time a use case is added, which defeats the purpose of the abstraction.

Provider independence belongs one level lower — a single narrow primitive:

```
IStructuredCompletion
    CompleteAsync<T>(PromptSpec spec, JsonSchema schema, CancellationToken ct) → Result<T>
```

One implementation per provider. Above it sit ordinary application services
(`AnswerEvaluator`, `ResumeAnalyzer`) that own their own prompts and output types.
Swapping providers touches one class rather than seven signatures.

### 3.3 Rules for AI output

- Structured JSON, schema-validated on the way in. An LLM response that fails schema
  validation is an error, not a value.
- AI **never writes a competency score directly.** It produces *evidence records*
  with a source type of `AiEvaluation` and a reliability weight below that of a human
  or deterministic signal. The scoring function decides what that is worth.
- Every AI-derived record keeps provenance: model, prompt version, timestamp.
- Generated content enters as `Draft` and is served only after human approval.

---

## 4. Security model

### 4.1 Authentication

ASP.NET Core Identity with JWT access tokens plus refresh tokens. No external IdP —
see ADR-0008. The application consumes claims, so moving behind Keycloak or Entra
later does not touch business code.

- Argon2id or ASP.NET Core Identity's default hasher; no custom crypto.
- Refresh tokens: rotated on use, stored hashed, revocable, family-invalidated on
  reuse detection (a replayed refresh token invalidates the whole chain).
- Short access-token lifetime (~15 min); refresh tokens ~14 days.
- Signing keys from `dotnet user-secrets` locally, environment variables in
  deployment. Never in `appsettings.json`.

### 4.2 Authorization and data ownership

SunBloom does **not** use classic multi-tenancy. The spec's own requirement —
"users have isolated data, sharing is explicit, family members cannot see each other's
progress by default" — describes per-user ownership, not tenant isolation. A college
does not own a student's skill profile the way a company owns its CRM rows.

So:

- Every personal table carries `owner_user_id`.
- An **EF global query filter** applies the current user automatically. Bypassing it
  requires an explicit, reviewable call.
- Organizations (Phase 7) are *membership plus explicit grants*, not data owners.
- Only **content** tables carry a nullable `organization_id`, so a training
  organization can eventually author private career paths.

The query filter is in place from the first migration. Retrofitting ownership across
every table later is the kind of change that quietly leaves one table behind, and the
table it leaves behind is the one that leaks.

### 4.3 Baseline controls

Input validation at the boundary (FluentValidation) · rate limiting on auth endpoints
and any future AI endpoints · RFC 9457 Problem Details errors that never echo internal
detail · audit log for auth events and permission changes · no PII in logs · uploaded
files (Phase 6) stored outside the web root, content-type verified, never executed.

---

## 5. API design

REST, versioned at `/api/v1/`.

- **Success responses return the resource directly.** No `{ success, data, error }`
  envelope — HTTP already carries that, and envelopes make generated clients worse.
- **Errors use RFC 9457 Problem Details**, with a stable machine-readable `type` per
  error class. This *is* the consistent error contract §35 asks for.
- **Lists return `{ items, nextCursor }`** with cursor pagination by default. Offset
  pagination is permitted only for small bounded catalogs.
- **No EF entity is ever serialized.** DTOs always.
- OpenAPI document generated from the code and committed, because the UI repo's
  TypeScript types are generated from it.

---

## 6. Scalability strategy

Design for horizontal scale; do not build for it yet.

**Now — free, structural:** stateless API (no in-process session state) · every list
endpoint paginated from day one · indexes designed with the schema · `ICacheService`
abstraction backed by `IMemoryCache` · background work through the outbox rather than
inline in requests.

**When measured need appears:**

| Signal | Response |
|---|---|
| >1 API instance needed | Swap `ICacheService` to Redis; move rate-limit state out of process |
| Scoring recomputation slows requests | Precompute scores into a read model on `EvidenceRecorded` |
| Skill-graph traversal slows | Add a materialized closure table |
| Content reads dominate | CDN in front of static content; cache approved career versions |
| One module dominates load | Extract it — contracts already exist; see ADR-0006 for the coupling to unwind |

**Explicitly deferred:** Redis, Docker, message brokers, microservices, OpenSearch,
read replicas, sharding. Each is a real answer to a real problem, and none of those
problems exist yet. See ADR-0008.

The scoring model is the one place where scale is designed in early: because
competency is *derived* rather than stored, a recomputation is always possible, and a
precomputed read model can be added without changing the source of truth.

---

## 7. Observability

Structured logging (Serilog) with correlation IDs · OpenTelemetry traces and metrics
from the start, exported to console locally · `/health/live` and `/health/ready`
(readiness checks the database) · every scoring computation logs its algorithm version.

That last one is not optional. When a score changes and a user asks why, the answer
must be recoverable from logs rather than reconstructed by guesswork.

---

## 8. What this architecture deliberately does not do

- No microservices. See ADR-0001.
- No CQRS with separate read/write databases. Single database; read models added only
  where measured.
- No event sourcing for general entities. The evidence ledger is append-only because
  the *domain* is append-only, not as a persistence pattern applied system-wide.
- No repository interface over `DbContext` for its own sake. EF's `DbSet` is already
  an abstraction; a second one adds indirection without adding a seam.
- No GraphQL. REST with generated clients is sufficient for one known consumer.
