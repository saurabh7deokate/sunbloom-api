# SunBloom — Domain Model

**Status:** Design · **Date:** 2026-08-16

Detailed for slice 1. Later-phase entities are sketched only where they constrain a
slice-1 decision.

---

## 1. Aggregates

An aggregate is a consistency boundary: everything inside it commits together, and
references *across* aggregates are by ID only — never by navigation property.

| Aggregate | Root | Contains |
|---|---|---|
| **User** | `User` | Credentials, profile, refresh tokens |
| **Skill** | `Skill` | Name, description, parent link, content provenance |
| **SkillRelationship** | *(standalone)* | Typed edge between two skills |
| **CareerPath** | `CareerPath` | Versions, each with skill requirements |
| **Resource** | `Resource` | Learning material bound to a skill |
| **UserCareerGoal** | `UserCareerGoal` | Target path version, target date, weekly minutes |
| **SkillEvidence** | *(append-only record)* | One observation about one user + skill |
| **Assessment** | `Assessment` | A point-in-time readiness snapshot |

---

## 2. Catalog

### 2.1 Skill

Skills form **one global graph shared by all career paths**. They are not owned by a
career path. This is the most important modelling decision in the Catalog module.

If each career path generated its own skills, an LLM would produce "LINQ", "LINQ
Queries", and "Language Integrated Query" across three paths, and every cross-path
comparison — job matching, career switching, readiness against a second target —
would silently compare unrelated nodes. See ADR-0004.

```
Skill
  Id, Slug (unique, canonical)
  Name, Description
  ParentSkillId?          ← single containment hierarchy
  Kind                    Area | Technology | Concept | Practice
  ContentProvenance       (see §5)
  IsActive                soft delete
  CreatedAt, UpdatedAt
```

`ParentSkillId` models the containment tree from the spec's `.NET → C# → LINQ` example.
Exactly one parent. This is the *structural* hierarchy.

### 2.2 SkillRelationship

Everything that is **not** containment is a typed edge, because those relationships
cross the tree. "Async/await" is a prerequisite for "ASP.NET Core performance" even
though they live in different branches — a tree cannot express that.

```
SkillRelationship
  FromSkillId, ToSkillId
  Type        Prerequisite | Related | Alternative
  Strength    0.0–1.0     how strongly it applies
```

**Invariant:** `Prerequisite` edges must form a DAG. A cycle makes sequencing
impossible and would hang the recommendation walk. Postgres cannot enforce this
declaratively, so it is validated in the application on write by walking the
existing graph before insert.

`Related` and `Alternative` are symmetric and may cycle freely.

### 2.3 CareerPath and versioning

Career requirements change year over year, and a historical assessment must remain
reproducible against the requirements it was taken against. So identity and content
are separated:

```
CareerPath                    stable identity — ".NET Backend Developer"
  Id, Slug, Name, Description

CareerPathVersion             the versioned content
  Id, CareerPathId
  Label                       "2026"
  SeniorityLevel              Junior | Mid | Senior | Lead
  Status                      Draft | Approved | Archived
  ReadinessWeights            per-component weights (see SCORING.md)
  PublishedAt

CareerSkillRequirement        what this version demands
  CareerPathVersionId, SkillId
  Dimension                   which of the four dimensions
  RequiredLevel               0–5
  Importance                  Critical | Core | Supporting
```

**Invariant:** once a `CareerPathVersion` is `Approved`, it and its requirements are
immutable. Changes create a new draft version. See ADR-0010.

`Importance` is what makes gap ranking meaningful — without it, every gap looks
equally urgent and the recommendation output degrades into an unordered list.

### 2.4 Resource

The minimum needed to make a recommendation actionable rather than merely diagnostic.

```
Resource
  Id, SkillId
  Title, Url
  Type              Article | Video | Course | Documentation | Exercise | Book
  EstimatedMinutes  ← required; the daily planner cannot work without it
  Difficulty        0–5
  ContentProvenance
```

`EstimatedMinutes` is mandatory. A plan that fits a user's available time is the whole
point of §13, and it is unbuildable if duration is optional.

---

## 3. Competency — the centre

### 3.1 Dimensions

Four scored dimensions:

```
Knowledge          Can they explain the concept?
PracticalAbility   Can they build with it?
ProblemSolving     Can they solve problems using it?
InterviewAbility   Can they explain it under interview conditions?
```

The spec listed *Evidence* as a fifth. It is not a dimension — it is corroboration,
and it modulates **confidence** in the other four rather than being averaged with them.

The spec's own example confirms this: Knowledge 4.2, Practical 4.5, Problem Solving
3.8, Interview 3.5 → Overall **4.0**, which is exactly `(4.2+4.5+3.8+3.5)/4`. Evidence
("Strong") is not in that mean. Modelling it as a fifth score would contradict the
example it appears in.

### 3.2 SkillEvidence — append-only

**The single most important table in the system.**

```
SkillEvidence
  Id
  OwnerUserId
  SkillId
  Dimension?                null = applies to all dimensions
  SourceType                SelfAssessment | Quiz | Practice | Interview
                            | Project | Certification | AiEvaluation
  SourceRefId?              the originating record
  ObservedLevel             0–5
  SelfReportedConfidence?   0–1, when the source is the user
  OccurredAt                when the observation happened, not when it was written
  RetractsEvidenceId?       tombstone pointer
  Metadata                  jsonb
  CreatedAt
```

Rules:

- **No `UPDATE`. No `DELETE`.** Enforced by convention in code and by a database
  trigger. A correction is a new row. A retraction is a row with
  `RetractsEvidenceId` set.
- `OccurredAt` is distinct from `CreatedAt` because time decay must reflect when the
  user actually demonstrated something, not when the system recorded it — important
  for imported or backdated evidence.
- There is **no score column anywhere.** Scores are computed. See ADR-0002.

This design makes three requirements fall out for free:

| Requirement | How it is satisfied |
|---|---|
| §19 "why does SunBloom think I'm strong?" | Query the evidence — it is already the answer |
| §8 versioned scoring | Change the function, recompute; source data is untouched |
| §24 reproducible history | Evidence is immutable, so any past score is re-derivable |

### 3.3 Assessment

A point-in-time snapshot, taken against a specific career path version.

```
Assessment
  Id, OwnerUserId
  CareerPathVersionId       ← snapshotted, not "current"
  ScoringAlgorithmVersion   ← snapshotted
  OverallReadiness          0–100
  ComponentScores           jsonb, with per-component confidence
  TakenAt
```

Both snapshot columns are what make §24 real. Without them, a scoring change silently
rewrites history and last month's "67% ready" becomes unexplainable.

---

## 4. Identity and goals

```
User
  Id, Email (unique), PasswordHash
  DisplayName, TimeZone
  CreatedAt, UpdatedAt, IsActive

UserCareerGoal
  Id, OwnerUserId
  CareerPathVersionId
  TargetDate?
  WeeklyMinutesAvailable    feeds the daily planner
  Status                    Active | Achieved | Abandoned
  CreatedAt
```

A user may hold multiple goals; exactly one may be `Active` at a time in slice 1.

---

## 5. Content provenance

Applied to every generated entity — `Skill`, `SkillRelationship`, `Resource`,
`CareerSkillRequirement`. Not retrofittable, because it must describe content at the
moment of generation.

```
ContentProvenance          (owned value object)
  GenerationSource         Human | Ai
  GeneratorModel?
  GeneratorPromptVersion?
  GeneratedAt?
  ReviewState              Draft | InReview | Approved | Rejected
  ReviewedByUserId?, ReviewedAt?
  ReviewNotes?
```

**Only `Approved` content is served to learners.** The generator writes `Draft`.

Generation is **hierarchical**: generate the ~8 top-level areas for a path, approve
them, then generate children only beneath approved nodes. Reviewing 400 flat nodes
leads to rubber-stamping, which defeats the point of human review; reviewing 8, then
40, then leaves is tractable and kills bad branches before their subtrees exist.

---

## 6. Invariants worth testing

1. `Prerequisite` edges form a DAG.
2. A skill's `ParentSkillId` chain never cycles.
3. `SkillEvidence` rows are never updated or deleted.
4. An `Approved` `CareerPathVersion` and its requirements never change.
5. Every personal row has a non-null `OwnerUserId`.
6. `RequiredLevel` and `ObservedLevel` are within 0–5.
7. A `Resource` always has a positive `EstimatedMinutes`.
8. At most one `Active` `UserCareerGoal` per user.

These are the tests that protect business behaviour, as opposed to tests that protect
line coverage.
