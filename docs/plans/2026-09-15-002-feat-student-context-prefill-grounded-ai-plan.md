---
title: "feat: Student evidence bundle — never-blank documents, carry-forward with provenance, AI grounded in authorized student context with citations"
type: feat
status: completed
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 2/8
depends_on: docs/plans/2026-09-15-001-feat-authoring-spine-template-editor-ai-parent-access-plan.md
---

# feat: Student evidence bundle, prefill and grounded AI (combined findings C02, C09-shared-profile)

## Problem Statement

`DocumentInstanceService.CreateAsync` writes `ValuesJson = "{}"`. A new IEP starts blank even though the student's name, DOB, grade, disability, school and team are known and a prior finalized IEP/ETR usually exists. `IepAssistService` sees only the draft text — it cannot cite a baseline, a prior goal, an ETR finding or a parent/student contribution, so it either hedges or invents. Parent-side `ChildProfile` and school-side `SchoolStudent` hold overlapping facts with no rule about which is authoritative, and the student workspace is only reachable through "pull from student".

## Decisions (defaults)

1. **Evidence bundle is a service-side, role-filtered projection** (`IStudentEvidenceService.BuildAsync(userId, schoolStudentId)`), not a stored blob. It returns typed `EvidenceItem`s with `{ id, kind, sourceType, sourceId, sourceLabel, sourceDate, authorRole, text, visibility }`. Kinds: `Identity`, `TeamMember`, `PriorGoal`, `PriorService`, `PriorAccommodation`, `PresentLevels`, `EtrFinding`, `StudentVoice`, `ParentContribution`.
2. **Sources:** `SchoolStudent` (+ plan 3 fields when present), active `SchoolStudentAccess` (team), latest finalized `AuthoredDocumentVersion` per document type (goals/services/accommodations/present levels resolved via semantics + `_rowId`), latest legacy `IepVersion` if no authored version exists, `StudentWorkspaceEntry` where `IsShareable`, and a new `ParentContribution` entity (parent-authored "About my child": strengths, concerns, what works at home, priorities) shared explicitly.
3. **Privacy:** parent prep notes, analyses, advocacy goals and non-shareable student entries are never included. The bundle is built with the *acting user's* access; parent AI (plan 6) builds a different bundle.
4. **Prefill on create** fills semantic fields: `studentProfile` (name, DOB, grade, disability, school, district, case manager/team when available), `presentLevels` from latest ETR/IEP present-levels **as a proposed block wrapped in a provenance banner text** ("Carried from IEP v2, finalized 2025-10-14"), goals/services/accommodations rows carried with `_rowId` preserved (lineage continuity) and `_carriedFrom: { versionId, rowId }`. Nothing is silently identical: carried goal rows are flagged `stale` in the editor until edited or explicitly confirmed ("Keep as-is").
5. **Citations:** assist responses return `{ suggestion, rationale, citations: [{ evidenceId, sourceLabel, excerpt }] }` and the prompt forbids uncited numeric claims; when no baseline exists the model must say so and the UI shows "No baseline on record — add one or ask the provider".

## Proposed Solution

### Backend
- `ParentContribution` entity (`ChildProfileId`, `Kind` enum: Strength|Concern|WorksAtHome|Priority|Other, `Text`, `IsShared`, audit fields) + migration; `ParentContributionService` (CRUD for parent, read-shared for staff via `ChildLink`); endpoints `GET/POST/PUT/DELETE /api/children/{childId}/contributions`, `GET /api/educator/students/{id}/contributions`.
- `StudentEvidenceService` + `GET /api/educator/students/{id}/evidence` (for the editor's Evidence drawer).
- `DocumentInstanceService.CreateAsync` → after template resolution, call `IDocumentPrefillService.BuildInitialValuesAsync(templateVersion, evidence)` producing values keyed by semantics; `_carriedFrom` preserved in row objects (validator whitelists `_rowId`, `_carriedFrom`, `_confirmed`).
- `DocumentAssistService` (plan 1) → include the bundle (capped, most-relevant-first) in the user message inside data tags; parse citations from a JSON-shaped completion; fall back to plain text on parse failure.

### Web
- `document-authoring/components/evidence-drawer.tsx`: right-side drawer listing evidence grouped by kind with source/date; "Insert" into the focused field; per-item privacy badge.
- Provenance chips on prefilled fields/rows ("From ETR 2024-03-02", "Carried from IEP v2 — Keep / Revise"); stale rows highlighted in the completeness panel.
- Assist suggestion panel shows citations; "no baseline" affordance.
- Parent: `children/:childId/overview` gains an "About {child} at home" card (contributions CRUD, share toggle with explicit "Visible to the school team" label).

## Implementation Phases

### Phase 1 — Evidence bundle + parent contributions
- Entity/migration/service/endpoints; evidence service; tests for role filtering (parent-private items excluded; unshared student entries excluded; revoked link excluded).
- Checkpoint: `GET /evidence` returns identity + prior goals + shared voice for a seeded student.

### Phase 2 — Prefill on create with provenance
- Prefill service; validator whitelist of `_carriedFrom`/`_confirmed`; editor chips + Keep/Revise; completeness panel "stale carry-forward" item.
- Tests: new IEP for a student with a finalized IEP carries goals with same `_rowId`; student with no history gets identity only; ETR present levels flow into IEP.
- Checkpoint: New IEP opens populated.

### Phase 3 — Grounded assist with citations + evidence drawer
- Bundle in assist/chat prompts; citation parsing; drawer + insert; no-baseline path.
- Tests: fake Claude returns citations → rendered; returns prose → degraded gracefully; prompt-injection text in a student entry stays inside data tags (assert prompt structure).
- Checkpoint: "Improve this goal" cites the ETR baseline; a goal with no baseline yields the explicit request for data.

## Acceptance Criteria

- [x] New IEP/ETR/504 documents open with identity, team and (where they exist) prior goals/services/accommodations/present levels prefilled, each labelled with source and date. *(Live: new OH IEP for a student with a finalized IEP v1 opened with profile + the carried goal.)*
- [x] Carried goal rows keep their `_rowId` from the prior version; stale rows are visible until confirmed or edited. *(Chip "Carried from IEP v1 · not yet reviewed" + Keep as-is; completeness lists unreviewed carry-forward.)*
- [x] Parent can add/edit/share "About my child" contributions; shared ones appear in staff evidence and AI context; unshared never do.
- [x] AI assist output includes citations to evidence items; missing baseline is stated, not invented. *(Live: SuggestMeasurement/Improve returned rationale + citations [E1, E3]. The parser accepts JSON, repairs raw newlines, and resolves inline [E#] markers in prose.)*
- [x] Evidence drawer lists role-filtered items and can insert into the focused field.
- [x] `dotnet test` 519, type-check, vitest 127, build, guard:ux pass; lint at the 37 pre-existing errors.

## Implementation notes (2026-09-15)

- Migration `AddParentContributions` applied to the QA database.
- Evidence and prefill are optional constructor dependencies of `DocumentInstanceService`/`DocumentAssistService` so existing tests and any caller without them keep the plain behaviour; prefill failures log a warning and create an empty draft.
- ETRs never inherit present levels (evaluation writes them); an ETR/504 carries rows only from a prior version of the same type.
- `AssistMaxTokens` raised to 2048 for grounded replies (JSON + rationale).

## Operational validation notes (for ship)

- **Runtime impact:** document create now runs the evidence bundle (≈6 queries) and prefill; assist calls include an evidence block (≤9k chars). Watch create latency and "Prefill failed"/"Evidence bundle unavailable" warnings.
- **Healthy signal:** new IEPs for students with history open with a populated profile and carried rows; assist responses carry `citations`.
- **Failure/mitigation:** a burst of prefill warnings → documents still create empty (no user-facing failure); revert web to hide the drawer if needed. Owner: Brad; window: first educator session after deploy.

## System-Wide Impact

- Prefill runs inside create — keep it fast (two queries); failures degrade to blank with a logged warning, never block create.
- `AuthoredDocumentPdfDocument` must not print `_carriedFrom`/`_confirmed`.
- Plan 7's `GoalRecord` projection relies on `_rowId` continuity established here.

## Dependencies & Risks

- Depends on plan 1 semantics and `_rowId`.
- Prompt size: cap bundle to ~6k tokens; prioritise identity, current-doc goals, prior goals, ETR findings, then voice.

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) C02, C09; personas P4 (Steph) trust rules, P1/P2 privacy boundary
- Code: `DocumentInstanceService.CreateAsync`, `IepAssistService` prompts, `StudentWorkspaceService.GetShareableEntriesForStudentAsync`, `ChildLinkService`
