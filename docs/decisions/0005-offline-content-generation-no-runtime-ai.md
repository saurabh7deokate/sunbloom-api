# ADR-0005 — Content generated offline and human-reviewed; no runtime AI before Phase 5

**Status:** Accepted · **Date:** 2026-08-16

## Context

SunBloom needs a large body of content: skill graphs, requirements, resources, and later
topics and questions. One .NET path alone is 150–400 skill nodes. Hand-authoring caps the
product at one or two paths; this is the project's largest risk (RISKS.md R1).

The owner chose AI generation with human curation. The question is *when* generation runs.

## Decision

**Content is generated offline by `tools/SunBloom.ContentGenerator` and reviewed by a
human before it is served. There is no AI in the runtime through Phase 4.**

- The generator is a console application, not referenced by the API host.
- It writes content in `Draft` state with full provenance: model, prompt version,
  timestamp.
- Only `Approved` content is served to learners.
- Generation is **hierarchical**: generate top-level areas → approve → generate children
  of approved nodes only.

## Consequences

**Positive — the largest simplification available to this project.** No provider SDK in
the host, no API keys in the API, no latency or cost on the request path, and no failure
mode where a user's dashboard depends on a third party being up. §40's entire concern
defers honestly rather than by hand-waving.

**Positive.** Hierarchical generation makes review tractable. Reviewing 400 flat nodes
leads to rubber-stamping, which defeats the point of curation; reviewing 8, then 40, then
leaves kills bad branches before their subtrees exist.

**Positive.** Provenance and review state are in the schema from the first migration —
not retrofittable, since they describe content at the moment of generation.

**Negative.** Content cannot adapt per user. Personalization is a Phase 5 concern and is
not foreclosed.

**Negative.** Review is real, unavoidable human work, and it is the project's throughput
limit. Named explicitly as R1 rather than discovered later.

## Alternatives rejected

**Generate at request time.** Latency, cost per view, non-deterministic content, no
review gate, and an external dependency in the critical path — to personalize content
that does not vary much per user anyway.

**Generate and auto-approve.** Removes the only defence against plausible-but-wrong
taxonomies (RISKS.md R5). A wrong prerequisite edge silently blocks a learnable skill.

**Import a public taxonomy (O*NET/ESCO).** Considered and rejected during design: these
are labour-market taxonomies, too coarse for the fine-grained technical hierarchy §7
requires. Still viable later as a seed for *breadth* across many paths.
