---
title: "feat: IEP Version Comparison — Diff Goals, Services, and Red Flags Across Versions"
type: feat
status: completed
date: 2026-03-14
origin: docs/brainstorms/2026-03-14-platform-features-brainstorm.md
---

# feat: IEP Version Comparison — Diff Goals, Services, and Red Flags Across Versions

## Overview

When parents have multiple IEPs for a child (annual reviews, amendments), they need to see what changed. This feature provides a structural diff of goals (added/removed/modified), service changes, a timeline view showing IEP evolution, and whether previously flagged red flags were addressed. The comparison is computed server-side from parsed Goal entities and IepSection data — no LLM call required for the core diff. An optional LLM summary of the most significant changes is available.

(see brainstorm: `docs/brainstorms/2026-03-14-platform-features-brainstorm.md`, Feature 3)

## Problem Statement / Motivation

IEPs change yearly and parents often can't tell what shifted. Subtle reductions in services, removed goals, or weakened criteria go unnoticed. The current app shows each IEP in isolation — there's no way to see evolution over time.

## Proposed Solution

### Three capabilities:

1. **Structural Goal Diff** — Compare goals between two parsed IEPs. Match goals by domain/text similarity. Show: added goals, removed goals, modified goals (with field-by-field changes).
2. **Section Diff** — Compare section types present in each IEP. Flag missing sections, new sections, and highlight service/accommodation changes.
3. **Timeline View** — Chronological list of all IEPs for a child showing meeting date, type, and high-level change summary.

### Comparison Approach (per brainstorm decision)

**Structural diff of parsed entities** — not an LLM-powered semantic diff. This is faster, cheaper, and more predictable. Goals are matched by domain + text similarity (fuzzy matching). Changes are computed field-by-field.

**Optional LLM summary** — After the structural diff is computed, an optional Claude call can summarize the most significant changes in plain language. This is a nice-to-have, not the core feature.

### How Goal Matching Works

Goals don't have stable IDs across IEP versions (they're parsed fresh from each PDF). Matching uses:
1. **Exact domain match** — if two goals share the same `Domain` value, they're candidates
2. **Text similarity** — among domain-matched candidates, compare `GoalText` using a simple Levenshtein distance or substring containment
3. **Unmatched goals** — goals in the newer IEP with no match are "added"; goals in the older IEP with no match are "removed"
4. **Matched goals with differences** — compare `Baseline`, `TargetCriteria`, `MeasurementMethod`, `Timeframe` field-by-field

### Data Model

No new database entities needed. The comparison is computed on-the-fly from existing `IepSection`, `Goal`, and `IepAnalysis` data. Results are returned directly from the API — not persisted.

### API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/children/{childId}/iep-timeline` | Timeline view: all IEPs for a child with date, type, and summary stats |
| GET | `/api/ieps/{id}/compare/{otherId}` | Full comparison between two specific IEPs |

**Timeline response:**
```json
{
  "childId": 1,
  "ieps": [
    {
      "id": 5,
      "iepDate": "2026-03-01",
      "meetingType": "annual_review",
      "status": "parsed",
      "goalCount": 6,
      "sectionCount": 8,
      "redFlagCount": 3,
      "hasAnalysis": true
    }
  ]
}
```

**Comparison response:**
```json
{
  "olderIepId": 3,
  "newerIepId": 5,
  "olderDate": "2025-03-01",
  "newerDate": "2026-03-01",
  "goalChanges": {
    "added": [{ "goalText": "...", "domain": "..." }],
    "removed": [{ "goalText": "...", "domain": "..." }],
    "modified": [{
      "domain": "Reading",
      "olderGoalText": "Read 60 wpm...",
      "newerGoalText": "Read 90 wpm...",
      "changes": [
        { "field": "TargetCriteria", "older": "60 wpm", "newer": "90 wpm" },
        { "field": "Baseline", "older": null, "newer": "Currently reading at 45 wpm" }
      ]
    }]
  },
  "sectionChanges": {
    "added": ["transition"],
    "removed": [],
    "inBoth": ["annual_goals", "services", "accommodations", "placement", ...]
  },
  "redFlagResolution": {
    "resolved": [{ "title": "Missing baseline", "wasInOlder": true, "addressedInNewer": true }],
    "persisting": [{ "title": "Vague measurement", "wasInOlder": true, "addressedInNewer": false }],
    "newFlags": [{ "title": "Service hours reduced", "wasInOlder": false }]
  },
  "summary": {
    "goalsAdded": 2,
    "goalsRemoved": 1,
    "goalsModified": 3,
    "goalsUnchanged": 2,
    "sectionsAdded": 1,
    "sectionsRemoved": 0,
    "redFlagsResolved": 2,
    "redFlagsPersisting": 1,
    "newRedFlags": 1
  }
}
```

## Technical Approach

### Phase 1: Backend — Comparison Service + Endpoints

**New files:**

| File | Description |
|------|-------------|
| `api/IepAssistant.Services/Models/IepComparisonModels.cs` | All comparison DTOs: GoalChange, SectionChange, RedFlagResolution, ComparisonResult, TimelineEntry |
| `api/IepAssistant.Services/Interfaces/IIepComparisonService.cs` | Interface |
| `api/IepAssistant.Services/Implementations/IepComparisonService.cs` | Core comparison logic: goal matching, section diff, red flag resolution tracking |
| `api/IepAssistant.Api/Controllers/IepComparisonController.cs` | Timeline + comparison endpoints |

**Modified files:**

| File | Change |
|------|--------|
| `api/IepAssistant.Services/DependencyInjection.cs` | Register IIepComparisonService |

**Goal matching algorithm (in `IepComparisonService`):**
1. Load sections + goals for both IEPs
2. Group goals by Domain (nullable — goals without domain go into an "unmatched" pool)
3. Within each domain group, find best text match using normalized Levenshtein distance
4. Threshold: >70% similarity = match, otherwise treat as added/removed
5. For matched goals: compare Baseline, TargetCriteria, MeasurementMethod, Timeframe

**Red flag resolution tracking:**
1. Load completed analysis for both IEPs (if available)
2. Deserialize OverallRedFlags JSON from both
3. Match red flags by title similarity
4. Classify: resolved (in older, not in newer), persisting (in both), new (only in newer)

**Simple Levenshtein distance** — implement as a small utility method (~20 lines). No NuGet package needed. Normalize as `1 - (distance / max(len1, len2))` to get a 0-1 similarity score.

### Phase 2: Frontend — Timeline + Comparison Views

**New files:**

| File | Description |
|------|-------------|
| `web/src/features/iep-comparison/api/iep-comparison-api.ts` | API client |
| `web/src/features/iep-comparison/hooks/use-iep-timeline.ts` | Hook for timeline data |
| `web/src/features/iep-comparison/hooks/use-iep-comparison.ts` | Hook for comparison data |
| `web/src/features/iep-comparison/components/iep-timeline.tsx` | Vertical timeline with IEP cards |
| `web/src/features/iep-comparison/components/comparison-view.tsx` | Full comparison layout |
| `web/src/features/iep-comparison/components/goal-diff-card.tsx` | Single goal diff display (added/removed/modified) |
| `web/src/features/iep-comparison/components/section-diff.tsx` | Section presence diff |
| `web/src/features/iep-comparison/components/red-flag-resolution.tsx` | Red flag resolution tracking |
| `web/src/features/iep-comparison/components/comparison-summary.tsx` | Stats summary bar |

**Modified files:**

| File | Change |
|------|--------|
| `web/src/types/api.ts` | Add comparison types |
| `web/src/features/children/components/child-detail-page.tsx` | Add Timeline section |
| `web/src/features/iep-documents/components/iep-viewer-page.tsx` | Add "Compare" button that lets user select another IEP to compare against |
| `web/src/app/routes.tsx` | Add `/children/:childId/compare/:iepId/:otherId` route |

**Timeline UI:**
- Vertical timeline with connecting line
- Each IEP as a card: meeting date, type badge, goal count, red flag count
- "Compare" link between adjacent entries
- Most recent at top

**Comparison UI:**
- Side-by-side layout: older IEP (left) vs newer IEP (right)
- Summary stats bar at top (goals added/removed/modified, flags resolved)
- Goal diff cards: green highlight for added, red for removed, amber for modified with field-by-field changes
- Section diff: simple list with +/- indicators
- Red flag resolution: green checkmark for resolved, amber for persisting, red for new

**Goal diff card (modified):**
```
┌─────────────────────────────────────────────────┐
│ READING                             Modified     │
│                                                   │
│ Goal: Read grade-level text at [60→90] wpm...    │
│                                                   │
│ Changes:                                          │
│   Target Criteria: 60 wpm → 90 wpm      ✓ Better │
│   Baseline: (none) → 45 wpm             ✓ Added  │
│   Timeframe: unchanged                           │
└─────────────────────────────────────────────────┘
```

## Acceptance Criteria

### Functional Requirements

- [ ] Timeline view shows all IEPs for a child in chronological order
- [ ] Each timeline entry shows: meeting date, type, goal count, section count, red flag count
- [ ] Parent can select two IEPs to compare
- [ ] Comparison shows goals: added, removed, modified with field-by-field changes
- [ ] Modified goals highlight specific field changes (baseline, target, measurement, timeframe)
- [ ] Comparison shows sections: added, removed, present in both
- [ ] Red flag resolution tracking: resolved, persisting, new
- [ ] Summary stats bar with counts
- [ ] Comparison works for any two parsed IEPs (not just adjacent ones)
- [ ] Handles IEPs with no analysis gracefully (skip red flag section)
- [ ] Handles IEPs with different section structures
- [ ] Goals matched by domain + text similarity (>70% threshold)

### Non-Functional Requirements

- [ ] Comparison computed on-the-fly — no persistence needed
- [ ] Comparison response in <2 seconds for typical IEPs
- [ ] No LLM call required (structural diff only)
- [ ] Brand UI components used throughout
- [ ] All endpoints have `[Authorize]` with ownership validation

## Dependencies & Risks

**Dependencies:** Requires at least two parsed IEPs for a child (comparison button disabled otherwise)

**Risks:**
- Goal matching accuracy: domain-based matching works well when domains are consistent. If one IEP uses "Reading" and another uses "Literacy", they won't match. The Levenshtein threshold of 70% mitigates minor variations.
- Red flag matching is approximate since flag titles may differ between analyses.

## Sources & References

### Origin
- **Brainstorm:** [docs/brainstorms/2026-03-14-platform-features-brainstorm.md](docs/brainstorms/2026-03-14-platform-features-brainstorm.md) — Feature 3: IEP Version Comparison. Key decisions: structural diff (not semantic LLM diff), optional LLM summary deferred, compare goals field-by-field.

### Internal References
- Goal entity: `api/IepAssistant.Domain/Entities/Goal.cs`
- IepSection entity: `api/IepAssistant.Domain/Entities/IepSection.cs`
- IepDocument entity: `api/IepAssistant.Domain/Entities/IepDocument.cs`
- IepAnalysis entity: `api/IepAssistant.Domain/Entities/IepAnalysis.cs`
- Document repository ordering: `api/IepAssistant.Domain/Repositories/IepDocumentRepository.cs`
- Frontend types: `web/src/types/api.ts`
