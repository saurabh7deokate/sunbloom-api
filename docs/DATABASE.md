# SunBloom — Database Design

**PostgreSQL 18** · one database, one schema per module · **Status:** Design

DDL below is design intent, not migration source. Migrations are generated from EF
Core model configuration; this document is what those configurations must produce.

---

## 1. Conventions

| Rule | Reason |
|---|---|
| `snake_case` tables and columns | Postgres convention; avoids quoting |
| `uuid` primary keys, **v7** | Time-ordered, so index locality is preserved unlike v4 |
| `timestamptz` always, never `timestamp` | Users span time zones; naive timestamps corrupt decay maths |
| `created_at`, `updated_at` on mutable tables | Audit baseline per §28 |
| `is_active` soft delete on catalog content | Deleting a skill would orphan evidence |
| `owner_user_id` on every personal table | Ownership model — §4.2 of ARCHITECTURE.md |
| No cross-schema FK *except* those listed in §6 | Extraction points, deliberately chosen |

Schemas: `identity`, `catalog`, `competency`, `shared`.

---

## 2. `identity`

```sql
CREATE TABLE identity.users (
    id                uuid PRIMARY KEY,
    email             citext NOT NULL UNIQUE,
    password_hash     text   NOT NULL,
    display_name      text   NOT NULL,
    time_zone         text   NOT NULL DEFAULT 'UTC',
    email_confirmed   boolean NOT NULL DEFAULT false,
    is_active         boolean NOT NULL DEFAULT true,
    created_at        timestamptz NOT NULL,
    updated_at        timestamptz NOT NULL
);

CREATE TABLE identity.refresh_tokens (
    id            uuid PRIMARY KEY,
    user_id       uuid NOT NULL REFERENCES identity.users(id) ON DELETE CASCADE,
    token_hash    text NOT NULL UNIQUE,      -- hashed; never store the token itself
    family_id     uuid NOT NULL,             -- rotation chain
    expires_at    timestamptz NOT NULL,
    revoked_at    timestamptz,
    replaced_by   uuid REFERENCES identity.refresh_tokens(id),
    created_at    timestamptz NOT NULL
);

CREATE INDEX ix_refresh_tokens_user_active
    ON identity.refresh_tokens (user_id) WHERE revoked_at IS NULL;
```

`family_id` implements reuse detection: presenting an already-rotated token revokes
the entire family, on the assumption it was stolen.

`citext` for email makes uniqueness case-insensitive at the database level rather than
relying on every call site to normalize.

---

## 3. `catalog`

### 3.1 Skills

```sql
CREATE TABLE catalog.skills (
    id                uuid PRIMARY KEY,
    slug              text NOT NULL UNIQUE,     -- canonical: 'csharp-linq'
    name              text NOT NULL,
    description       text,
    parent_skill_id   uuid REFERENCES catalog.skills(id),
    kind              text NOT NULL,            -- Area|Technology|Concept|Practice
    is_active         boolean NOT NULL DEFAULT true,

    -- content provenance (owned value object)
    generation_source        text NOT NULL,     -- Human|Ai
    generator_model          text,
    generator_prompt_version text,
    generated_at             timestamptz,
    review_state             text NOT NULL,     -- Draft|InReview|Approved|Rejected
    reviewed_by_user_id      uuid,
    reviewed_at              timestamptz,
    review_notes             text,

    created_at        timestamptz NOT NULL,
    updated_at        timestamptz NOT NULL
);

CREATE INDEX ix_skills_parent   ON catalog.skills (parent_skill_id);
CREATE INDEX ix_skills_approved ON catalog.skills (id)
    WHERE review_state = 'Approved' AND is_active;
```

The partial index matters: every learner-facing query filters to approved and active,
which is a small subset of rows once generation is running.

### 3.2 Skill relationships

```sql
CREATE TABLE catalog.skill_relationships (
    id             uuid PRIMARY KEY,
    from_skill_id  uuid NOT NULL REFERENCES catalog.skills(id) ON DELETE CASCADE,
    to_skill_id    uuid NOT NULL REFERENCES catalog.skills(id) ON DELETE CASCADE,
    type           text NOT NULL,          -- Prerequisite|Related|Alternative
    strength       numeric(3,2) NOT NULL DEFAULT 1.0,
    created_at     timestamptz NOT NULL,

    CONSTRAINT uq_skill_rel UNIQUE (from_skill_id, to_skill_id, type),
    CONSTRAINT ck_skill_rel_no_self CHECK (from_skill_id <> to_skill_id)
);

CREATE INDEX ix_skill_rel_from ON catalog.skill_relationships (from_skill_id, type);
CREATE INDEX ix_skill_rel_to   ON catalog.skill_relationships (to_skill_id, type);
```

Both directions are indexed because prerequisite traversal runs forwards (*what does
this need?*) and backwards (*what does this unlock?* — the `unlockCount` term in gap
ranking).

**Acyclicity of `Prerequisite` edges is enforced in application code**, not by the
database. Postgres cannot express it declaratively; the write path walks existing
edges before inserting. See DOMAIN-MODEL.md §2.2.

> **Rejected alternative:** `ltree`. It models a single hierarchy path elegantly but
> cannot express prerequisites that cross branches — the exact case that matters most.
> Adjacency plus recursive CTEs handles both. A materialized closure table is the
> escalation path if traversal is measured slow, not before.

### 3.3 Career paths

```sql
CREATE TABLE catalog.career_paths (
    id           uuid PRIMARY KEY,
    slug         text NOT NULL UNIQUE,
    name         text NOT NULL,
    description  text,
    is_active    boolean NOT NULL DEFAULT true,
    created_at   timestamptz NOT NULL,
    updated_at   timestamptz NOT NULL
);

CREATE TABLE catalog.career_path_versions (
    id               uuid PRIMARY KEY,
    career_path_id   uuid NOT NULL REFERENCES catalog.career_paths(id),
    label            text NOT NULL,          -- '2026'
    seniority_level  text NOT NULL,          -- Junior|Mid|Senior|Lead
    status           text NOT NULL,          -- Draft|Approved|Archived
    readiness_weights jsonb NOT NULL,        -- component -> weight
    published_at     timestamptz,
    created_at       timestamptz NOT NULL,

    CONSTRAINT uq_cpv UNIQUE (career_path_id, label, seniority_level)
);

CREATE TABLE catalog.career_skill_requirements (
    id                      uuid PRIMARY KEY,
    career_path_version_id  uuid NOT NULL REFERENCES catalog.career_path_versions(id)
                                 ON DELETE CASCADE,
    skill_id                uuid NOT NULL REFERENCES catalog.skills(id),
    dimension               text NOT NULL,   -- Knowledge|PracticalAbility|...
    required_level          numeric(3,2) NOT NULL,
    importance              text NOT NULL,   -- Critical|Core|Supporting

    CONSTRAINT uq_csr UNIQUE (career_path_version_id, skill_id, dimension),
    CONSTRAINT ck_csr_level CHECK (required_level BETWEEN 0 AND 5)
);

CREATE INDEX ix_csr_version ON catalog.career_skill_requirements (career_path_version_id);
```

**Immutability of approved versions is enforced by a trigger**, not by trusting
application code — this is the guarantee that makes historical assessments
reproducible, and it is worth defending at the lowest possible level.

### 3.4 Resources

```sql
CREATE TABLE catalog.resources (
    id                uuid PRIMARY KEY,
    skill_id          uuid NOT NULL REFERENCES catalog.skills(id),
    title             text NOT NULL,
    url               text NOT NULL,
    type              text NOT NULL,        -- Article|Video|Course|Documentation|...
    estimated_minutes int  NOT NULL,
    difficulty        numeric(3,2),
    organization_id   uuid,                 -- null = global content (Phase 7)

    generation_source text NOT NULL,
    review_state      text NOT NULL,
    reviewed_by_user_id uuid,
    reviewed_at       timestamptz,

    created_at        timestamptz NOT NULL,
    updated_at        timestamptz NOT NULL,

    CONSTRAINT ck_resource_minutes CHECK (estimated_minutes > 0)
);

CREATE INDEX ix_resources_skill ON catalog.resources (skill_id)
    WHERE review_state = 'Approved';
```

`estimated_minutes > 0` is a database constraint because the daily planner divides by
it. A zero would not be bad data — it would be a crash.

---

## 4. `competency`

### 4.1 The evidence ledger

```sql
CREATE TABLE competency.skill_evidence (
    id                       uuid PRIMARY KEY,
    owner_user_id            uuid NOT NULL,
    skill_id                 uuid NOT NULL,
    dimension                text,             -- null = all dimensions
    source_type              text NOT NULL,    -- SelfAssessment|Quiz|Practice|...
    source_ref_id            uuid,
    observed_level           numeric(3,2) NOT NULL,
    self_reported_confidence numeric(3,2),
    occurred_at              timestamptz NOT NULL,
    retracts_evidence_id     uuid REFERENCES competency.skill_evidence(id),
    metadata                 jsonb NOT NULL DEFAULT '{}',
    created_at               timestamptz NOT NULL,

    CONSTRAINT ck_evidence_level CHECK (observed_level BETWEEN 0 AND 5)
);

-- The hot path: score computation for one user's skill.
CREATE INDEX ix_evidence_scoring
    ON competency.skill_evidence (owner_user_id, skill_id, dimension, occurred_at DESC);

-- Timeline / "why am I rated this?" views.
CREATE INDEX ix_evidence_user_time
    ON competency.skill_evidence (owner_user_id, occurred_at DESC);

-- Append-only, enforced in the database rather than by convention.
CREATE RULE evidence_no_update AS ON UPDATE TO competency.skill_evidence DO INSTEAD NOTHING;
CREATE RULE evidence_no_delete AS ON DELETE TO competency.skill_evidence DO INSTEAD NOTHING;
```

The rules are the point. Append-only is the load-bearing property of the entire
scoring model, and a rule that lives only in code survives exactly as long as everyone
remembers it — which, on a project built across many independent sessions, is not long.

> This table will become the largest in the system. It is also the one most amenable
> to partitioning by `occurred_at` if it ever needs it. Nothing in the design blocks
> that later.

### 4.2 Goals and assessments

```sql
CREATE TABLE competency.user_career_goals (
    id                      uuid PRIMARY KEY,
    owner_user_id           uuid NOT NULL,
    career_path_version_id  uuid NOT NULL,
    target_date             date,
    weekly_minutes_available int NOT NULL DEFAULT 300,
    status                  text NOT NULL,      -- Active|Achieved|Abandoned
    created_at              timestamptz NOT NULL,
    updated_at              timestamptz NOT NULL
);

-- At most one active goal per user.
CREATE UNIQUE INDEX uq_active_goal ON competency.user_career_goals (owner_user_id)
    WHERE status = 'Active';

CREATE TABLE competency.assessments (
    id                        uuid PRIMARY KEY,
    owner_user_id             uuid NOT NULL,
    career_path_version_id    uuid NOT NULL,     -- snapshotted
    scoring_algorithm_version text NOT NULL,     -- snapshotted
    overall_readiness         numeric(5,2) NOT NULL,
    component_scores          jsonb NOT NULL,    -- incl. per-component confidence
    taken_at                  timestamptz NOT NULL
);

CREATE INDEX ix_assessments_user ON competency.assessments (owner_user_id, taken_at DESC);
```

The partial unique index enforces "one active goal" in the database — a business
invariant that would otherwise depend on a race-prone read-then-write.

---

## 5. `shared`

```sql
CREATE TABLE shared.outbox_messages (
    id             uuid PRIMARY KEY,
    type           text NOT NULL,
    payload        jsonb NOT NULL,
    occurred_at    timestamptz NOT NULL,
    processed_at   timestamptz,
    attempts       int NOT NULL DEFAULT 0,
    error          text
);

CREATE INDEX ix_outbox_pending ON shared.outbox_messages (occurred_at)
    WHERE processed_at IS NULL;
```

---

## 6. Cross-schema foreign keys — deliberate coupling

Strict modular-monolith practice forbids cross-module FKs so modules can be extracted
independently. That purity is rejected here, for a reason worth stating plainly:

- Orphaned evidence pointing at a deleted skill would **silently corrupt every score**
  derived from it, and would be near-impossible to detect after the fact.
- The probability of needing to extract a module in the next two years is low.

Expected value favours integrity. These FKs are therefore permitted, and enumerated
here so extraction is a known, bounded task rather than an archaeological one:

| From | To | Unwind by |
|---|---|---|
| `competency.skill_evidence.skill_id` | `catalog.skills.id` | Drop FK; add reconciliation job |
| `competency.user_career_goals.career_path_version_id` | `catalog.career_path_versions.id` | Drop FK; validate in application |
| `competency.assessments.career_path_version_id` | `catalog.career_path_versions.id` | Drop FK; validate in application |
| `competency.*.owner_user_id` | `identity.users.id` | Keep — identity will not be extracted |

Catalog content is **soft-deleted**, never hard-deleted, so these FKs are rarely
exercised in practice. See ADR-0006.

---

## 7. Ownership enforcement

Every table with `owner_user_id` gets an EF Core **global query filter** binding it to
the current user. Bypassing it requires an explicit `IgnoreQueryFilters()` call, which
is greppable and reviewable.

An architecture test asserts that **every entity implementing `IOwnedByUser` has a
corresponding query filter configured** — so a new personal table cannot be added
without ownership enforcement, even by an author who has never read this document.
