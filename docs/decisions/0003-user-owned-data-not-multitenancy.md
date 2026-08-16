# ADR-0003 — User-owned data with explicit grants, not multi-tenancy

**Status:** Accepted · **Date:** 2026-08-16

## Context

The spec (§21) asks for multi-tenancy, listing tenants as individual, family, student
group, college, training organization, community, and company.

But it then describes the requirement as: *users have isolated data · sharing is
explicit and permission-based · do not assume family members can see each other's
private progress.*

That is **per-user ownership**, not tenant isolation. Classic multi-tenancy isolates one
organization's data from another's and assumes the organization owns the rows. That
model is wrong here: a college does not own a student's skill profile the way a company
owns its CRM records. The student does, and keeps it after leaving.

## Decision

- Every personal table carries **`owner_user_id`**, with an EF Core global query filter
  applied from the first migration.
- Organizations (Phase 7) are **membership plus explicit sharing grants**, never data
  owners.
- Only **content** tables carry a nullable `organization_id`, so a training organization
  can eventually author private career paths.
- No organization, group, grant, or role tables are built in slice 1 — only the
  ownership column and its filter.

## Consequences

**Positive.** Matches the privacy model the spec actually describes. Cheaper than
row-level tenancy. The expensive, irreversible half — the ownership column and filter —
is in place from day one, while the speculative half is deferred.

**Positive.** An architecture test asserts every `IOwnedByUser` entity has a query
filter configured, so a new personal table cannot skip ownership even if its author has
never read this ADR.

**Negative.** An organization that wants mandatory visibility into member progress —
a college tracking placement readiness — needs a grant model that users can be required
to accept at join time. Deferred to Phase 7, unblocked by this decision.

## Alternatives rejected

**Classic `tenant_id` on every row.** Wrong ownership semantics, and it would make the
common case — one individual — carry a concept that exists only for organizations.

**Defer ownership entirely, add it later.** The one genuinely expensive retrofit here.
Adding an ownership column across every table later reliably misses one table, and the
table it misses is the one that leaks.
