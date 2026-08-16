# SunBloom — Scoring, Gap Ranking, and Planning

**Status:** Design · **Algorithm version:** `v1` · **Date:** 2026-08-16

This is the heart of the product. Everything users see — readiness, gaps, the daily
plan — is produced here. It is also the part most likely to be wrong at first, which
is exactly why it is versioned and derived rather than stored.

> **Design rule:** every number shown to a user must be explainable by pointing at the
> evidence and the formula that produced it. If a number cannot be explained, it does
> not ship.

---

## 1. From evidence to a dimension score

For a given user, skill, and dimension, gather all non-retracted evidence and compute
a weighted mean:

```
score(u, s, d) = Σ (wᵢ × levelᵢ) / Σ wᵢ

where  wᵢ = reliability(sourceTypeᵢ) × recency(occurredAtᵢ, d) × selfConfidenceᵢ
```

### 1.1 Source reliability — v1

How much a kind of observation is trusted. Self-assessment is deliberately the
weakest signal, because §9 is right that self-ratings are unreliable.

| Source | Reliability | Reasoning |
|---|---:|---|
| `Interview` | 0.85 | Performance under pressure, externally judged |
| `Practice` | 0.80 | Demonstrated, objectively scored |
| `Quiz` | 0.70 | Objective but narrow; recognition ≠ recall |
| `Certification` | 0.65 | Verified but often shallow and stale |
| `Project` | 0.60 | Strong practical signal, but self-attested |
| `AiEvaluation` | 0.50 | Useful, not authoritative — Phase 5 |
| `SelfAssessment` | 0.35 | Honest but systematically miscalibrated |

### 1.2 Recency decay

Skills decay. Knowledge fades faster than the ability to build things.

```
recency(t, d) = 0.5 ^ (ageInDays(t) / halfLife(d))
```

| Dimension | Half-life | Reasoning |
|---|---:|---|
| `Knowledge` | 180 d | Facts and syntax fade fastest |
| `ProblemSolving` | 240 d | Transferable, decays moderately |
| `InterviewAbility` | 180 d | Highly practice-dependent |
| `PracticalAbility` | 365 d | Muscle memory persists |

Decay has a floor of `0.15` — someone who genuinely built production systems in .NET
five years ago is not a beginner, and a model that says so is wrong in a way users
will immediately notice and distrust.

### 1.3 Confidence — the honesty mechanism

Confidence is **not** the score. It states how much the score should be believed.

```
confidence(u, s, d) = (1 − e^(−Σwᵢ / k)) × diversityBonus

k = 1.5
diversityBonus = 1 + 0.15 × (distinctSourceTypes − 1),  capped at 1.3
```

A single self-assessment yields `≈0.21` confidence — deliberately low. It takes
corroboration from independent sources to become believable.

**This is where §8's "Evidence" belongs.** It is not a fifth dimension; it is the
corroboration that raises confidence in the other four.

**UI contract:** the API returns confidence with every score, and the UI must show
low-confidence scores as provisional. In slice 1 — where self-assessment is the *only*
source — nearly everything is low-confidence, and the interface must say so plainly.
A product that presents unverified self-ratings as measured fact is lying to a user
who is making career decisions with it.

### 1.4 Overall skill score

Unweighted mean of the four dimensions, matching the spec's own arithmetic:

```
overall(u, s) = (knowledge + practical + problemSolving + interview) / 4
```

Dimensions with **no evidence at all** are excluded and the mean renormalized — they
are *unmeasured*, not zero. Treating unmeasured as zero would make every new user
look hopeless and every partial assessment look like failure.

---

## 2. Career readiness

Per §20, readiness is transparent and component-wise — never a single opaque number.

```
readiness(u, v) = Σ (weight_c × component_c) / Σ weight_c      → 0–100
                  over components with sufficient data only
```

| Component | Source | Available in slice 1 |
|---|---|:--:|
| Skill Knowledge | `Knowledge` vs. requirements | ✅ |
| Practical Ability | `PracticalAbility` vs. requirements | ✅ |
| Problem Solving | `ProblemSolving` vs. requirements | ✅ |
| Interview Readiness | `InterviewAbility` vs. requirements | ⬜ Phase 5 |
| Project Evidence | Corroborating evidence coverage | ⬜ Phase 3 |
| Job Match | Job description comparison | ⬜ Phase 6 |

Weights live on `CareerPathVersion.ReadinessWeights` — a senior backend role weights
system design and problem solving far more heavily than a junior one, and that belongs
in data rather than code.

**Unavailable components are excluded and the remainder renormalized**, and the API
reports which were excluded. They are never counted as zero. This is the difference
between "74% ready on the three things we can measure" and a meaningless 37%.

Per-component score against requirements:

```
component_c = 100 × Σ (importanceWeight(s) × min(1, actual(s,d) / required(s,d)))
                  / Σ importanceWeight(s)

importanceWeight:  Critical = 3.0 · Core = 2.0 · Supporting = 1.0
```

Capping each skill's ratio at 1 stops strength in one area from masking a total gap in
another — being outstanding at C# should not paper over knowing no SQL.

---

## 3. Gap ranking — where the product earns its keep

Naive gap analysis produces useless advice: *"you are weak at Docker → study Docker."*
Nobody needs software for that. The value is in **sequencing**, **prioritization**, and
**fitting the time actually available**.

### 3.1 Blocked vs. ready

A gap whose prerequisites are unmet is **blocked** and must not be recommended.
Telling someone to study Kubernetes before they understand containers is worse than
saying nothing — it produces failure the user reads as their own.

```
blocked(s) = ∃ p ∈ prerequisites(s) : overall(u, p) < requiredLevel(p) − 0.5
```

Blocked skills are surfaced, but shown as *"blocked by: Docker fundamentals"* — which
turns the prerequisite graph into visible guidance rather than a hidden filter.

### 3.2 Priority

Among unblocked gaps:

```
priority(s) = (readinessImpact(s) × gapSize(s) × urgency) / effort(s)

readinessImpact(s) = importanceWeight(s) × componentWeight(d)
gapSize(s)         = required(s,d) − actual(s,d)
effort(s)          = Σ EstimatedMinutes of approved resources for s   (default 120)
urgency            = 1 + unlockCount(s) × 0.2
```

`unlockCount` — how many other required skills list `s` as a prerequisite — is what
makes the ordering genuinely non-obvious. A moderate gap that unblocks four downstream
skills should outrank a larger gap that unblocks nothing, and no user working from a
flat "you're weak at X" list would ever work that out for themselves.

### 3.3 Worked example

```
Target: Mid-Level .NET Developer (2026) — readiness 67%

  1. SQL indexing          gap 1.5 · Critical · unlocks 3 · 60 min   → 12.4  ◄ top
  2. Docker fundamentals   gap 2.5 · Core     · unlocks 2 · 180 min  →  7.1
  3. System design basics  gap 3.0 · Critical · unlocks 0 · 600 min  →  4.5
  ─ blocked ────────────────────────────────────────────────────────────────
     Kubernetes            blocked by: Docker fundamentals
     Microservices         blocked by: System design basics, Docker fundamentals
```

System design is the *largest* gap and still ranks third, because it is expensive and
unlocks nothing immediately. That ordering is the product's actual output — and it is
not something the user could have produced from a list of weaknesses.

---

## 4. The daily plan

Given `WeeklyMinutesAvailable` and today's remaining budget, fill the time greedily by
value density (`priority / minutes`), then apply three shaping rules:

1. **Mix activity types.** Never fill 60 minutes with one modality — the plan should
   not be three consecutive videos.
2. **Revision first** (Phase 3). Scheduled revision outranks new learning; retention
   beats accumulation.
3. **Leave a short tail.** Cap the final item at the remaining minutes rather than
   overflowing, so an honest 60-minute plan takes 60 minutes.

```
Today — 60 minutes

  20 min   Revise SQL indexing              ← highest priority, unlocks 3
  20 min   2 SQL optimization problems      ← different modality, same skill
  15 min   Docker fundamentals: images      ← next unblocked gap
   5 min   3 .NET interview questions       ← retrieval practice
```

Greedy knapsack is sufficient at this scale. Optimality is not the constraint; the
constraint is that the plan feels sensible and finishes on time.

---

## 5. Versioning and recomputation

Every constant on this page belongs to algorithm version `v1` and is stored in
configuration, not scattered through code.

- Every `Assessment` records the `ScoringAlgorithmVersion` that produced it.
- Changing any constant means a **new version** — never an in-place edit.
- Because scores are derived from immutable evidence, historical scores can be
  recomputed under any version, and versions can be compared directly.

This is the payoff for refusing to store a score column. A scoring change is a
recomputation instead of a data migration, and no history is destroyed.

---

## 6. Honest limitations of v1

Written down deliberately, so they are neither forgotten nor rediscovered as surprises:

1. **Self-assessment is the only source in slice 1.** Scores are exactly as accurate
   as the user's self-knowledge. Confidence values will be low, and the UI must say so.
2. **All constants are guesses.** Reliability weights and half-lives are plausible, not
   empirical. They should be revisited once there is real data.
3. **Effort estimates depend on resource coverage.** A skill with no approved resources
   falls back to a 120-minute default, which will misrank it.
4. **No difficulty personalization.** Two users with identical scores get identical
   plans, regardless of learning speed.
5. **The prerequisite graph is AI-generated.** A wrong edge silently blocks a skill the
   user could have learned. Blocked items must always show *why*, so the user can
   notice when the graph is wrong.
