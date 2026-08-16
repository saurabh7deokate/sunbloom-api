# ADR-0009 — Angular signals in feature-scoped services, not NgRx

**Status:** Accepted · **Date:** 2026-08-16

## Context

The Angular frontend needs state management for server data (skills, scores, plans), UI
state, and session state. Options range from NgRx with actions/reducers/effects, through
NgRx SignalStore, to plain signals in services.

Angular's signals are mature as of v21 and the framework is moving toward them as the
default reactivity primitive.

## Decision

**Signals in feature-scoped services.** No NgRx.

- Server state: a signal store per feature, **provided at the route**, not in `root`.
- UI state: component-local signals.
- Session state: one `AuthStore` in `core/`, application-wide.
- Derived values via `computed()`. Effects for genuine side effects only — never for
  deriving state.

## Consequences

**Positive.** Far less ceremony: no action/reducer/effect triple for what is a service
method and a signal. Fewer concepts for a solo developer to hold, and less generated code
for an agent to get subtly wrong.

**Positive.** Route-provided stores are disposed on navigation. Root-provided stores
accumulate stale data across a session and become a cache nobody invalidates.

**Negative.** No time-travel debugging or centralized action log. Acceptable — SunBloom's
state is mostly server-fetched read models, not complex client-side transitions.

**Negative.** No enforced structure. If cross-feature state coordination becomes genuinely
complex, revisit with NgRx SignalStore — the smaller step from here than full NgRx.

## Alternatives rejected

**NgRx (full).** Substantial boilerplate for a state shape that is mostly cached HTTP
responses. Justified by large teams needing enforced conventions; the enforcement value
does not apply to one developer.

**NgRx SignalStore.** A reasonable middle ground, and the designated escalation path.
Rejected for now only because plain signals are sufficient and simpler.

**Services with `BehaviorSubject`.** The pre-signals idiom. Signals do this better with
less code, and `computed()` removes most manual subscription management.
