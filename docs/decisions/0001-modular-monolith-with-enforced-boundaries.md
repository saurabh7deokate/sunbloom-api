# ADR-0001 — Modular monolith with compiler-enforced boundaries

**Status:** Accepted · **Date:** 2026-08-16

## Context

SunBloom is intended to scale eventually, but is being built by one part-time developer
driving an AI agent. The spec lists fifteen modules and warns against microservices.

Two failure modes are in tension. Too little structure and the codebase becomes a ball
of mud. Too much and fifteen near-empty projects with an `IRepository` each consume the
effort that should have gone into the product.

There is a third factor specific to this project: **the primary developer is an AI agent
with no memory between sessions.** Conventional boundaries — folder layout, naming rules,
"we agreed not to do that" — survive only as long as someone remembers them. Across many
independent sessions, they will not be remembered.

## Decision

A modular monolith, one deployable process, with these rules:

1. **One project per module**, not per layer. Layers are folders inside a module.
2. **Everything outside a module's `Contracts/` namespace is `internal`.** Other modules
   cannot reference internals — enforced by the C# access system, not by review.
3. **Four modules before Phase 3:** Identity, Catalog, Competency, Recommendations. A
   module is created when a boundary is felt, not when it is anticipated.
4. **Cross-module reads** go through contract query interfaces; **cross-module writes**
   go through domain events.
5. **Architecture tests fail the build** on boundary and layering violations.

## Consequences

**Positive.** Boundaries hold without anyone remembering them. Module extraction later is
mechanical, because contracts already exist. Project count stays proportionate to the work.

**Negative.** `internal` plus `InternalsVisibleTo` is slightly awkward for testing. Some
duplication across module contracts — accepted; shared DTOs across boundaries are how
modular monoliths quietly become distributed monoliths.

**Neutral.** Layering within a module is only test-enforced, not compiler-enforced. This
is a deliberate trade: twelve projects would be worse than a test that can be deleted.

## Alternatives rejected

**Microservices.** No scale requirement, no team to justify the coordination cost, and
distributed transactions across skills and evidence would be a self-inflicted wound.

**Folders in one project.** Zero enforcement. Given an agent developer with no session
memory, this is equivalent to no boundary at all — the specific reason the enforcement
mechanism was chosen over the convention.

**Fifteen modules from the spec.** Anticipated boundaries, not felt ones. Eleven of them
would be empty for months.
