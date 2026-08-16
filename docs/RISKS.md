# SunBloom — Risks

**Date:** 2026-08-16

Ordered by expected damage. The first three are the ones that decide whether this
project succeeds; the rest are manageable engineering concerns.

---

## R1 — Content volume is the real bottleneck, not code 🔴

One credible .NET career path is roughly 150–400 skill nodes plus relationships,
requirements, and resources. Every module in this architecture could be built
correctly and the product would still be empty.

The spec has no plan for this. It is the single largest risk to the project.

**Mitigation:** content generation tooling is a slice-1 deliverable (1.5), not a
Phase-7 afterthought. Hierarchical generation with top-down approval keeps review
tractable. One path is completed before a second is started.

**Leading indicator:** if sub-slice 1.5 takes more than about twice its estimate, the
sixteen-career-path ambition in §6 needs an explicit rethink rather than a quiet slip.

---

## R2 — Recommendations may be obvious rather than insightful 🔴

The core product promise is *"what should I do next?"* If the answer is "you're weak
at Docker, study Docker," the user did not need software to find that out.

**Mitigation:** value is designed into *sequencing* (prerequisite blocking),
*prioritization* (`unlockCount`, importance weighting, effort), and *time-fitting* —
see SCORING.md §3. The worked example there ranks the largest gap third, which is the
kind of output a user could not produce unaided.

**Leading indicator:** at sub-slice 1.8, if the owner looks at the ranked list and
thinks *"I knew that,"* the ranking model needs work before anything is built on top
of it. This is the cheapest possible moment to discover that.

---

## R3 — Scope versus capacity 🔴

The spec describes seven phases and sixteen career paths. It is being built by one
person, part-time, driving an AI agent. Phases 1–4 alone are many months of work.

The failure mode is not technical — it is reaching month six with excellent
architecture and nothing usable.

**Mitigation:** slice 1 is defined to be independently useful. If work stops
immediately afterwards, the owner still has a working personal tool. Every slice is
sequenced to preserve that property.

---

## R4 — Self-assessment is the only evidence source in slice 1 🟠

Scores will be exactly as accurate as the user's self-knowledge, which §9 correctly
says is unreliable. A product that presents self-ratings as measured fact misleads
someone making career decisions.

**Mitigation:** confidence is a first-class output, low by design for self-assessment
alone (≈0.21 for a single rating), and the UI is contractually required to show
provisional scores as provisional. See SCORING.md §1.3.

**Accepted:** slice 1 output is genuinely soft. That is honest and visible rather than
hidden behind a confident-looking number.

---

## R5 — AI-generated skill graphs will be plausible but subtly wrong 🟠

LLM-generated taxonomies produce near-duplicate nodes, arbitrary depth, and incorrect
prerequisite edges that look reasonable. A wrong prerequisite silently blocks a skill
the user could have learned.

**Mitigation:** global canonical skills with unique slugs (ADR-0004) · human approval
before content is served · blocked items always display *why*, so a wrong edge is
visible to the user rather than invisible · prerequisite cycles rejected on write.

---

## R6 — Two repositories will drift 🟠

API contracts and UI expectations diverge; nothing structural prevents it. This is the
known cost of the two-repo decision.

**Mitigation:** TypeScript types generated from the OpenAPI document, never
hand-written; regeneration required in the same session as a contract change (ADR-0007).

**Residual:** nothing enforces this across repos automatically. A CI check comparing
the committed OpenAPI document against the UI's generated types is the escalation if
drift actually occurs.

---

## R7 — Scoring constants are guesses 🟡

Every reliability weight and half-life in SCORING.md is plausible, not empirical.

**Mitigation:** this is precisely why scoring is versioned and derived. Constants live
in configuration; changing them is a recomputation, not a migration; assessments record
the version that produced them. The architecture makes being wrong cheap — which is the
correct response to a number nobody can yet know.

---

## R8 — Cross-schema foreign keys couple modules 🟡

A deliberate trade (ADR-0006): integrity now over extraction convenience later.

**Mitigation:** every such FK is enumerated in DATABASE.md §6 with its unwind path.

---

## R9 — No Docker means dev/prod parity gap 🟡

Development runs against a local Postgres install. Deployment eventually will not.

**Mitigation:** environment-based configuration from the start; nothing cloud-specific
in the domain. Containerization is deferred, not designed out (ADR-0008).

---

## R10 — Modular monolith erodes under agent-driven development 🟡

An AI agent with no memory of prior sessions will not remember conventions.

**Mitigation:** boundaries are compiler-enforced (`internal` + project references), not
conventional. Architecture tests fail the build. `CLAUDE.md` reconstitutes context every
session. This risk drove the enforcement mechanism (ADR-0001) rather than merely being
noted alongside it.

---

## Watch list — not yet risks

- **Skill graph traversal performance.** Recursive CTEs over a few thousand nodes are
  fine. Revisit with a closure table if measured slow.
- **Evidence table growth.** Append-only means monotonic growth. Partitioning by
  `occurred_at` is available and unblocked.
- **Career path versioning UX.** Migrating a user to a newer version of their target
  path is unmodelled. It becomes real in Phase 2.
- **Password reset and email delivery.** Deferred in slice 1; needed before anyone
  other than the owner uses this.
