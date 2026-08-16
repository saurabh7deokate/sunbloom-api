# ADR-0008 — Defer Redis, Docker, brokers, and an external IdP

**Status:** Accepted · **Date:** 2026-08-16

## Context

The spec asks for Redis (§29), Docker and Compose (§39), background workers (§30),
event-driven boundaries (§31), and secure authentication (§33) — while also warning
against introducing distributed infrastructure that is not justified (§25, §38).

The environment matters: **Docker is not installed** on the development machine.
PostgreSQL 18.4 runs natively. Redis on Windows without Docker means WSL or a
third-party build.

The product currently has zero users.

## Decision

Defer all of it, behind seams that make adoption cheap when a real signal appears.

| Deferred | Now | Adopt when |
|---|---|---|
| **Redis** | `ICacheService` over `IMemoryCache` | More than one API instance is needed |
| **Docker** | Native Postgres; environment-based config | Deploying, or a second developer joins |
| **Message broker** | In-process dispatcher + outbox table | Cross-service delivery is genuinely required |
| **External IdP** | ASP.NET Core Identity + JWT | Org SSO in Phase 7 |
| **OpenSearch** | Postgres full-text behind a search abstraction | Postgres search is measured insufficient |

The **outbox table exists from the start** even though there is no broker. Retrofitting
delivery guarantees onto an event system that already has consumers is significantly
harder than starting with one, and the table costs almost nothing.

## Consequences

**Positive.** No infrastructure to run, install, or debug before the product exists.
Every deferral has a named adoption trigger, so this is a scheduling decision rather
than an omission.

**Positive.** Because the application only consumes claims, moving behind Keycloak or
Entra later does not touch business code.

**Negative.** Dev/prod parity gap — development runs against a local Postgres install
(RISKS.md R9). Mitigated by environment-based configuration and nothing cloud-specific
in the domain.

**Negative.** In-memory cache does not survive restart and cannot be shared. Irrelevant
at one instance; the seam makes the swap a DI registration change.

## Alternatives rejected

**Set it all up now.** Days of infrastructure work before a single user-visible feature,
on a machine without Docker, to solve problems that do not exist. Directly contrary to
§44.

**Use nothing and add abstractions later.** Cheap to add `ICacheService` and an outbox
now; expensive to thread them through a codebase that grew without them.
