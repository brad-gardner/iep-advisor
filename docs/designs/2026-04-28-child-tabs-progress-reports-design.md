---
date: 2026-04-28
feature: Child detail tabs + Current IEP + Progress Reports + Goals in all analyses
---

# Design Discussion: Child detail tabs, Current IEP, Progress Reports

**Date:** 2026-04-28
**Feature:** Reorganize child detail into tabs (Overview / IEPs / ETRs / Goals); allow one IEP to be marked "current" per child; add Progress Reports attached to an IEP with goal-aware analysis; ensure parent advocacy goals are passed into IEP, ETR, and Progress Report analyses.

## Current State

- `web/src/features/children/components/child-detail-page.tsx` is a single scrolling page with stacked `Card` sections: Profile, Sharing, Advocacy Goals, IEPs, ETRs. No tab navigation.
- `web/src/app/routes.tsx` exposes `/children/:id` for the detail page and separate top-level routes `/ieps/:id`, `/etrs/:id`. No nested route under the child.
- `api/IepAssistant.Domain/Entities/IepDocument.cs` has no "current" flag. `ChildProfile.cs` has no `CurrentIepDocumentId`.
- `IepAnalysisService.AnalyzeDocumentAsync` already loads `ParentAdvocacyGoal` records via `_goalRepository.GetByChildIdAsync`, snapshots them on the analysis, and runs an advocacy-gap pass when goals exist (`api/IepAssistant.Services/Implementations/IepAnalysisService.cs:174-213`).
- `EtrAnalysisService.AnalyzeWithClaudeAsync` (`api/IepAssistant.Services/Implementations/EtrAnalysisService.cs:121`) does **not** load or pass advocacy goals — confirmed by absence of `IParentAdvocacyGoalRepository` injection.
- No `ProgressReport` entity, controller, service, or migration exists today.
- ETR feature is the most recent reference pattern (entity → repository → service → controller → React feature folder with viewer page + tabs for sections / analysis / meeting prep).

## Patterns to Follow

- **Backend feature shape:** mirror `EtrDocument` / `EtrAnalysis` — entity in `api/IepAssistant.Domain/Entities/`, repo in `Domain/Repositories/`, service + interface in `Services/`, controller in `Api/Controllers/`, models under `Services/Models/`. Soft delete via `IsActive`. Audit fields via `IAuditableEntity`.
- **EF migration:** one new migration `AddCurrentIepAndProgressReports`; follow existing migration naming (see `Domain/Data/Migrations/`).
- **Analysis pattern:** copy the IEP advocacy-goal flow from `IepAnalysisService.cs:174-213` — load goals, snapshot to analysis row, branch prompt on `hasParentGoals`, persist `AdvocacyGapAnalysis` and `ParentGoalsSnapshot` JSON columns.
- **React feature folder:** new `web/src/features/progress-reports/` with `api/`, `components/`, `hooks/` subfolders, matching `etr-documents/`. Reuse upload UX from `etr-upload.tsx` + `iep-upload.tsx`.
- **Tabs UI:** prefer the existing tab pattern from `etr-viewer-page.tsx` (it already renders three tabs: sections / analysis / meeting prep). Lift that into a small reusable `Tabs` primitive if one isn't already in `components/ui/`. Sync active tab to URL via React Router nested routes (`/children/:id/ieps`, `/children/:id/etrs`, etc.) so deep links and back/forward work.
- **Vertical slicing:** each phase ships full stack — DB → service → API → UI → manual verification — not all DB first.
- **Small components rule (global CLAUDE.md):** keep new components tightly scoped; don't bloat `child-detail-page.tsx` further.

## Desired End State

**Routing**
- `/children/:id` → `ChildDetailPage` (tab shell). Default tab = Overview.
- `/children/:id/overview` → Profile + Sharing cards.
- `/children/:id/ieps` → IEP list + create + "Current IEP" badge/toggle.
- `/children/:id/ieps/:iepId` → IEP viewer (replaces top-level `/ieps/:id`; old route 301-redirects).
- `/children/:id/ieps/:iepId/progress-reports/:prId` → progress report viewer.
- `/children/:id/etrs` → ETR list + create.
- `/children/:id/etrs/:etrId` → ETR viewer (replaces top-level `/etrs/:id` with redirect).
- `/children/:id/goals` → AdvocacyGoalsList in its own pane.

**Data**
- `ChildProfile.CurrentIepDocumentId` (nullable FK → `IepDocument.Id`, `OnDelete: SetNull`). Setting an IEP "current" updates this column. Auto-set when first IEP is created or when a newer IEP supersedes the current one (rule: most recent `IepDate ?? UploadDate` if no current is set).
- New entity `ProgressReport`: `Id`, `IepDocumentId` (FK, required), `ChildProfileId` (denormalized for query convenience and sharing checks), `FileName`, `BlobUri`, `UploadDate`, `ReportingPeriodStart`, `ReportingPeriodEnd`, `Status` (created/uploaded/processing/parsed/error), `Notes`, audit fields, `IsActive`.
- New entity `ProgressReportAnalysis`: `Id`, `ProgressReportId`, `Summary`, `GoalProgressFindings` (JSON — per-goal alignment/progress), `RedFlags` (JSON), `AdvocacyGapAnalysis` (JSON, when parent goals exist), `ParentGoalsSnapshot` (JSON), `IepGoalsSnapshot` (JSON — snapshot of IEP goals for context), `CreatedAt`.
- `EtrAnalysis` gains `AdvocacyGapAnalysis` (nullable JSON) and `ParentGoalsSnapshot` (nullable JSON) columns to mirror IEP analysis.

**Analysis behavior**
- IEP analysis: unchanged (already includes advocacy goals).
- ETR analysis: load advocacy goals via `IParentAdvocacyGoalRepository`, pass to Claude prompt, persist snapshot + advocacy gap.
- Progress Report analysis: load IEP goals (from the parent `IepDocument`'s sections) **and** parent advocacy goals; produce per-IEP-goal progress assessment plus advocacy alignment.

**UI**
- Child detail: tab shell with Overview / IEPs / ETRs / Goals tabs.
- IEPs tab: list of IEPs, "Current" badge on the active one, "Set as current" action on each row, inline "New IEP" button (existing).
- IEP viewer: new "Progress Reports" tab alongside today's tabs (sections / analysis / meeting prep). Allows upload + lists progress reports with status. Each row links to viewer.
- Progress Report viewer: sections + analysis tabs (subset of IEP viewer).
- Sharing roles: progress reports inherit access from the child (owner/editor can upload, viewer can read) — same rule as IEPs/ETRs, enforced via `IAccessService`.

## Design Decisions

1. **Tabs over scrolling stacked cards** — the page is getting long; tabs reduce cognitive load and give clean URLs for deep links.
2. **Current IEP at child level (single FK)** — chosen over a per-IEP `IsCurrent` boolean because there can only be one current; FK guarantees this without needing a unique-filter index. Rationale confirmed by user.
3. **Auto-set current on first/newest IEP, user-overridable** — most parents won't think to mark "current"; auto-defaulting matches the natural mental model. They can still pick a different one if they're reviewing an older draft.
4. **ProgressReport as its own entity (not a flag on `IepDocument`)** — different lifecycle, different fields (reporting period), different analysis output (goal-progress oriented). Reusing `IepDocument` would break queries that assume `IepDocument` = full IEP.
5. **ProgressReport requires an IEP** — per user's stated intent ("in the context of an IEP"). Default: attach to `CurrentIepDocumentId`. Override at upload time.
6. **Extend ETR analysis to include advocacy goals** — user explicitly asked for this. Mirror the IEP pattern; minimal new code. Existing ETR analyses do not auto-rerun, but a "re-analyze" action is already present.
7. **Snapshot parent goals + IEP goals on ProgressReportAnalysis** — analyses must remain stable even if a parent edits a goal later. Same rationale as the existing IEP `ParentGoalsSnapshot`.
8. **Nested routes, not query-param tabs** — React Router v7 handles this cleanly, gives shareable deep links, plays well with browser back/forward, and is the pattern already used elsewhere in the app (e.g. `/children/:childId/compare/:iepId/:otherId`).
9. **Old top-level routes (`/ieps/:id`, `/etrs/:id`) keep working as redirects** — links in old emails, bookmarks, and Sentry traces should not 404. Redirect server-side via React Router redirect routes that look up the `childProfileId` from the document and forward.
10. **No background re-analysis on goal change** — out of scope. Continue requiring a manual re-analyze. Document this in the plan as an explicit non-goal so reviewers don't ask.

## Open Questions

1. **Auto-rerun ETR analysis after this ships?** Existing rows lack the goal snapshot. Options: (a) leave as-is (parent re-runs manually), (b) one-time backfill during deploy, (c) automatic stale banner when goals exist but snapshot is empty. Recommendation: (c) — same `stale-analysis-banner` pattern already in IEP feature.
2. **Progress Report PDF parsing** — does Claude need a separate parsing prompt (different schema than IEP/ETR), or can we reuse the IEP PDF parsing service and just store sections? Recommendation: dedicated `ProgressReportParsingService` because progress reports are structured around per-goal progress, not sections.
3. **Reporting period detection** — auto-extract from PDF (Claude) or require parent to enter at upload? Recommendation: parent enters with a Claude-suggested default. Less prompt risk.
4. **"Current IEP" semantics for shared users (advocates / co-parents)** — does setting the current IEP affect all viewers, or is it per-user? Recommendation: child-level (affects everyone with access). Matches the "this IS the active IEP" mental model.
5. **Migration of existing analyses** — the new ETR snapshot columns are nullable; existing rows show "no goal data" until re-analyzed. Acceptable?

## Testing Strategy

- **Unit tests (.NET):** `ProgressReportService`, `ProgressReportAnalysisService`, ETR analysis goal-loading branch. xUnit + in-memory DB matching existing service tests.
- **Integration tests:** controller-level happy paths for upload / list / set-current / analyze. Real Postgres via existing test fixture.
- **EF migration test:** apply migration to a snapshot DB; verify `CurrentIepDocumentId` FK and `ProgressReports` schema; verify `Down` works.
- **Frontend unit tests:** tab routing, "set current" action, progress report upload form. Vitest + RTL matching existing feature tests.
- **Manual / browser:** full user flow — upload IEP → mark current → upload progress report → run analysis → confirm advocacy goals appear in output → switch tabs and verify URL updates → deep-link into a tab and verify it loads directly. Run dev server and exercise in browser before declaring done (CLAUDE.md rule).
- **Regression:** verify old `/ieps/:id` and `/etrs/:id` URLs still resolve (redirect to nested route).
