# Design Discussion: Unified analysis — one engine, two views

**Date:** 2026-10-02
**Feature:** Retire the per-document IEP/ETR analysis engines; `AnalysisRun` becomes the only analysis, viewed from the child page and from each document page.
**Origin:** `docs/brainstorms/2026-10-02-unified-analysis-navigation-brainstorm.md` — direction, scope (IEP + ETR, not progress reports), document-page view (latest run that includes the document) and goal ratings for every IEP were **decided during brainstorming**.

## Current State

- **Two engines.**
  - `AnalysisRunService` (child page, `POST api/children/{childId}/analysis-runs`) makes **one** Claude call for all sources (32k output cap, `AnalysisRunService.cs:174-179`). Its output is per-source sections, cross-doc synthesis, red flags and the advocacy gap. It has **no goal ratings**, even though goal IDs are already in the prompt (`:426-456`).
  - `IepAnalysisService` / `EtrAnalysisService` (document pages, `POST api/ieps/{id}/analyze`, `api/etrs/{id}/analyze`) overwrite **one row per document** in place (`IepAnalysisService.cs:137-150`, `EtrAnalysisService.cs:96-105`).
- **One-way copy.** `AnalysisRunBackfillService` copies legacy rows into runs once per boot, skipping any key that already exists (`:82-86`), so re-analyses never reach the run. It stores `GoalAnalyses` as an *array* section that cannot deserialize as `AnalysisRunSectionResult`. Backfilled goal ratings are therefore **invisible** on the child page today (`run-source-sections.tsx:19-23`).
- **No links in either direction.** A run's sources don't link to their documents. Document pages don't link to runs. The selected run is local state (`child-analysis-tab.tsx:23`), so it can't be linked by URL. Advocate citations land on `/children/{id}/analysis` with no run (`citation-links.ts:40-47`).
- **Legacy readers:**
  - meeting prep, Mode A (`MeetingPrepService.cs:367-370`) and ETR mode (`:350-353`);
  - advocate tools (`AdvocateToolset.cs:291-600`);
  - IEP comparison red-flag diff (`IepComparisonService.cs:56-60, 299-310`);
  - data export (`AccountService.cs:84,155`, which **exports neither runs nor ETR analyses**);
  - admin stats (`AdminController.cs:243-246`);
  - account purge (already covers runs).
- **Polling is inconsistent.** Runs poll for 5 min, IEP for 15 min, ETR for about 3 min. The run sweep happens only at startup.
- **Metering.** Runs and IEP analyses share the `"analysis"` limit (5 per child per year; a run costs 1). ETR analysis is unmetered (`CheckEtrAnalysisLimitAsync` always returns true).
- **Tests.**
  - `AnalysisRunServiceTests` (13), `AnalysisRunBackfillTests` (5), `AdvocateToolsetTests` (38), `MeetingPrepBackwardCompatTests` (5).
  - No legacy-service tests and no analysis component tests.
  - One e2e test (`e2e/tests/iep-analysis.spec.ts`) drives the legacy IEP analysis.

## Patterns to Follow

- **Usage:** reserve with `TryReserveUsageAsync` → release with `ReleaseUsageByIdAsync` *before* setting a terminal status (`AnalysisRunService.FailRunAsync`, `:641-682`, todos/P2-03 ordering).
- **Claude calls:** through `IClaudeClient.CompleteAsync`. Respect the output budget and the `max_tokens` warning (from the 2026-09-29 fixes). Never trust model-returned IDs: validate `goalId` against the source's goal set (`docs/solutions/logic-errors/2026-09-16-family-draft-sharing-untrusted-ids-in-prompts-…`).
- **Rendering:**
  - Goal ratings: `AnalysisGoalsList` / `AnalysisGoalCard` / `SmartCriteriaGrid` (`web/src/features/iep-documents/components/`) take `GoalAnalysis[]` and are engine-agnostic.
  - Run sections render via `RunSourceSections` → `AnalysisSectionDetail`.
  - Red flags via `RedFlagCard`; the gap via `AdvocacyGapAnalysisSection`.
- **Deep links:** `#goal-{id}` handling in `iep-viewer-page.tsx:45-60`.
- **Polling:** `usePolling` with an explicit `maxPolls` (`web/src/hooks/use-polling.ts`, added 2026-09-29).
- **Backfill:** batches, resumable, unique `BackfillSourceKey`.

## Desired End State

1. **One engine.** `AnalysisRun` is the only analysis for IEPs and ETRs. The legacy analyze endpoints, queues and workers are removed. `IepAnalyses` / `EtrAnalyses` stay as read-only history for one release, then a follow-up drops them.
2. **Per-source execution.** Each run makes **one Claude call per source** (an IEP call returns its sections plus a `goalAnalyses` block; an ETR call returns its sections plus completeness and eligibility), then **one synthesis call** when there are 2 or more sources. Each source records its own status. The run completes when its source calls and the synthesis are done.
3. **Typed section kinds** on `AnalysisRunSection`:
   - `iep_goals`: an object `{ goalAnalyses: [...] }`, only for goal IDs that belong to that source;
   - `etr_completeness`;
   - `etr_eligibility`;
   - ordinary sections as today.
4. **Child page.**
   - Run history, multi-source runs and run detail.
   - The selected run is in the URL (`/children/:childId/analysis?run=:runId`).
   - Each source in a run links to its document page.
   - IEP sources show goal ratings.
5. **Document page (IEP and ETR) → Analysis tab.**
   - Shows this document's slice of the **latest run that included it**: its sections, goal ratings (IEP) or completeness and eligibility (ETR), and red flags.
   - A note plus a link to the full run when other documents were included.
   - "Analyze this IEP/ETR" creates a single-source run.
   - A stale banner when the document was re-processed after the run, i.e. the run's goal IDs are missing from the current parse.
6. **Consumers read runs.**
   - Meeting prep: latest completed run including the chosen IEP/ETR.
   - Advocate tools: runs only.
   - IEP comparison: red flags from each IEP's latest run.
   - Data export: includes runs.
   - Admin stats: count runs.
7. **Final re-sync.** One upsert pass converts every legacy row into its run: it updates stale copies like run 15 and converts the `annual_goals` array and ETR snake_case into the new section kinds.

## Design Decisions

- **One call per source plus synthesis**, decided in brainstorming. Each call stays well inside the output budget, and goal ratings fit for every IEP.
- **A run still costs one unit of `"analysis"`**, however many calls it makes. It matches today and is the simplest rule for parents.
- **Partial failure:** if some source calls fail, the run completes with those sources marked failed. Synthesis runs over the sources that succeeded. The unit is refunded only if *every* source fails. A failed source can be retried by starting a new run.
- **The run id goes in the child-tab URL.** That enables document → run links and precise advocate citations.
- **Polling:** run polling moves to the 15-minute cap (180 × 5 s), matching IEP. A per-source progress line ("2 of 3 documents analyzed") reads the per-source status.
- **Runtime sweep:** the startup-only orphan sweep stays. Runs are additionally failed (and refunded) if they are still `Running` 30 minutes after starting, checked by the existing worker loop. Today a hung run is only cleaned up by a restart.
- **Billing user:** the child's owner, as runs already do. The legacy rule billed the document creator.
- **Goal IDs:** validated server-side against the source's parsed goals. Unknown IDs are dropped (and logged by count only).
- **Data export:** add runs (and their sections) to the export while touching this. It's a compliance gap today, and the legacy rows it does export are going away.

## Resolved Questions (2026-10-02)

1. **ETR metering:** ETR analyses count as `"analysis"`, one of the child's 5 per year, the same as IEPs. The `CheckEtrAnalysisLimitAsync` stub goes away.
2. **Partial failure:** the run completes with failed sources marked. Synthesis covers the sources that succeeded. The unit is refunded only if every source fails.
3. **Legacy tables:** `IepAnalyses` / `EtrAnalyses` become read-only after the final re-sync and are dropped in a follow-up release.

**Design approved:** 2026-10-02.

## Testing Strategy

- **Backend unit and integration tests** (SQLite fixture `AnalysisRunTestFixture`):
  - per-source execution with a fake `IClaudeClient`;
  - goal-ID validation;
  - partial-failure and refund rules;
  - section-kind persistence and read mapping;
  - document-scoped "latest run including document X" query;
  - re-sync upsert (stale copy updated, array → `iep_goals`, ETR conversion);
  - consumer switchovers (meeting prep, advocate tools, comparison, export).
- **Frontend (Vitest):**
  - document-view component states (none, running, completed, partial, stale, part of a multi-document run);
  - run URL selection;
  - source → document links;
  - goal ratings in run sections.
- **E2E:** rewrite `e2e/tests/iep-analysis.spec.ts` for "Analyze this IEP" → single-source run → the document view shows goal ratings → the link opens the same run on the child page.
- **Manual on QA:** re-analyze Jacob's IEP (document 35) from the document page; confirm the child timeline and the document page show the same run and run 15 is corrected by the re-sync.
