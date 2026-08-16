# SunBloom API

Backend for **SunBloom** — a career and skill-development platform that answers one
question:

> What should I do next to become better prepared for my target career?

Pick a target career → see the skills it requires → assess where you actually stand →
get your gaps ranked by impact and prerequisite order → get told what to work on today.

**Status:** Architecture complete, implementation not started.

## Documentation

| Document | Contents |
|---|---|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Modules, backend structure, AI seam, security, scalability |
| [DOMAIN-MODEL.md](docs/DOMAIN-MODEL.md) | Aggregates, entities, invariants |
| [DATABASE.md](docs/DATABASE.md) | PostgreSQL schema design |
| [SCORING.md](docs/SCORING.md) | Competency scoring, gap ranking, daily planning |
| [ROADMAP.md](docs/ROADMAP.md) | Vertical slices and phases |
| [RISKS.md](docs/RISKS.md) | Known risks, ordered by expected damage |
| [decisions/](docs/decisions/) | Architecture Decision Records |

Start with [decisions/README.md](docs/decisions/README.md) — the ADRs explain *why*
everything else looks the way it does.

## Design in one paragraph

A modular monolith on ASP.NET Core and PostgreSQL, with module boundaries enforced by
the compiler rather than by convention. Skill competency is never stored as a number —
it is derived from an append-only ledger of evidence through a versioned scoring
function, which is what lets the product explain every score it shows, revise its
scoring without destroying history, and reproduce any past assessment exactly.

## Stack

.NET 10 · ASP.NET Core · EF Core · PostgreSQL 18 · Angular 21 (in
[sunbloom-ui](https://github.com/saurabh7deokate/sunbloom-ui))

## Related

Frontend: [sunbloom-ui](https://github.com/saurabh7deokate/sunbloom-ui) — clone it as a
sibling directory.
