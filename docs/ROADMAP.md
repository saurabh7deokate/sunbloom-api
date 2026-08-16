# SunBloom — Development Roadmap

**Status:** Design · **Date:** 2026-08-16

The unit of work is a **vertical slice**: something a user can do, end to end, tested.
Each slice must be finishable *and verifiable* within a single working session —
a slice that spans sessions is where architectural drift enters.

---

## Slice 1 — ".NET Backend Developer, end to end"

**Goal:** a user signs up, picks .NET Backend Developer, rates their skills, and gets
a ranked, prerequisite-aware list of what to work on with resources attached.

**Chosen because** the owner can personally judge whether the generated .NET content
is any good. With AI-generated content, the reviewer *is* the quality gate — reviewing
a domain you don't know well is rubber-stamping with extra steps.

### Sub-slices

| # | Deliverable | Done when |
|---|---|---|
| **1.1** | Solution skeleton: 4 module projects, host, SharedKernel, `IModule` wiring, health checks, Serilog, OpenTelemetry, architecture tests, CI | `/health/ready` returns healthy; architecture tests run and pass in CI |
| **1.2** | Identity: register, login, JWT + refresh rotation, `IOwnedByUser` filter infrastructure | A user registers, logs in, calls an authenticated endpoint; a replayed refresh token revokes the family |
| **1.3** | Catalog schema + skill graph API; ~30 hand-authored .NET skills as a seed | Skill tree renders; recursive CTE returns a subtree; cycle rejection is tested |
| **1.4** | Angular shell: scaffold, auth, routing, layout, generated API types | Log in, see the skill tree, refresh the page and stay logged in |
| **1.5** | Content generator CLI + review workflow; expand to the full .NET tree | Generate → review → approve; only approved content is served |
| **1.6** | Evidence ledger + self-assessment + scoring v1 | Rate a skill, see the score *and its confidence*, see the evidence behind it |
| **1.7** | Career path versions, requirements, readiness assessment | Readiness shows per-component scores with unmeasured components labelled, not zeroed |
| **1.8** | Gap ranking with prerequisite blocking + `unlockCount` | Ranked gaps; blocked items show what blocks them |
| **1.9** | Resources + daily plan | A 60-minute plan that fits 60 minutes and mixes modalities |
| **1.10** | Dashboard answering the five questions from §37 | Where am I · where am I going · what am I weak at · what should I do today · am I improving |

**Slice 1 is complete when** the owner uses it for a real week without needing to
touch the database by hand.

### Explicitly not in slice 1

Topics and learning content · quizzes and practice · revision scheduling · interviews ·
AI at runtime · job descriptions · resumes · groups and organizations · Redis · Docker ·
email delivery · password reset (deferred to 1.11 — a real gap, tracked, not forgotten).

---

## Later phases

Named so work has somewhere to land. **Not designed** — designing them now would be
speculation against requirements that do not exist. Each gets its own design pass when
it is next, informed by what slice 1 teaches.

| Phase | Scope | Must not be foreclosed by earlier work |
|---|---|---|
| **2** | More career paths; skill graph at scale; content tooling maturity | Skills stay global and shared; paths reference them |
| **3** | Topics, learning progress, practice, quizzes, spaced revision | Evidence ledger accepts new source types without schema change |
| **4** | Recommendation depth, richer readiness, progress trends | Scoring stays versioned and recomputable |
| **5** | AI tutor, question generation, answer evaluation, mock interviews | `IStructuredCompletion` seam; AI writes evidence, never scores |
| **6** | Job descriptions, JD analysis, resume claim-vs-evidence | Skills are canonical, so external text maps onto one vocabulary |
| **7** | Groups, families, organizations, sharing grants | `owner_user_id` everywhere; grants layer on without migration |

---

## Sequencing rationale

Two orderings that are deliberate and worth not reversing:

**Content tooling (1.5) comes before scoring (1.6).** Scoring against 30 hand-seeded
skills proves the maths but not the product. Scoring against a realistic 300-node tree
reveals whether gap ranking produces genuine insight or restates the obvious — which is
the central product risk, and the one worth learning early while changing course is cheap.

**Readiness (1.7) comes before gap ranking (1.8).** Ranking needs importance weights and
requirements to rank *against*. Building it first would mean inventing a temporary
priority model and then throwing it away.

---

## Definition of done, per slice

1. Feature works end to end through the UI — not just via an HTTP client.
2. Domain invariants covered by tests (DOMAIN-MODEL.md §6).
3. Architecture tests pass.
4. No new `any` in TypeScript; no new warnings in C#.
5. Docs updated **if** a documented decision changed — and superseded by an ADR, not
   edited silently.
6. Committed with the personal git identity (verify: `git config user.email`).
