---
title: "fix: IEP meeting prep workflow improvements"
type: fix
status: completed
date: 2026-03-17
---

# fix: IEP Meeting Prep Workflow Improvements

## Overview

Six testing feedback items covering the IEP document processing and meeting prep workflow. The changes span three areas: relocating meeting prep to IEP-only context, shortening the checklist output, and fixing missing auto-polling for all async operations.

## Problem Statement / Motivation

1. **Meeting prep on child page is misplaced** — it appears on the main child detail page but makes more sense scoped to a specific IEP document. The child-level "goals only" mode generates generic content without IEP context.
2. **Meeting prep is too long** — 6 sections with 3-8 items each (18-48 items) overwhelms users. The most actionable content is questions to ask and red flags.
3. **File upload status stuck** — after uploading a PDF, the status shows "processing" forever because there's no polling. User must manually refresh.
4. **Analysis/meeting prep loading stuck** — same issue. After triggering analysis or meeting prep generation, the loading indicator never resolves without a refresh.
5. **IEP analysis in meeting prep context** — **already implemented**. Mode A (IEP-linked) meeting prep already includes analysis summary, red flags, and goal analyses in the Claude prompt. No changes needed.
6. **No way to regenerate meeting prep** — once a checklist is completed, there's no button to regenerate it (e.g., after uploading and analyzing a new document).

## Proposed Solution

### Item 1: Remove Meeting Prep from Child Detail Page

Remove the "Meeting Prep" `<Card>` section from `child-detail-page.tsx`. Meeting prep is already available on the IEP viewer's "Meeting Prep" tab — that's the right home for it.

**Existing Mode B (goals-only) checklists:** Leave them in the database. No migration needed — they simply become inaccessible from the UI. They can be cleaned up later if desired.

**Users with no parsed IEPs:** They won't have access to meeting prep until they upload and parse a document. This is acceptable — meeting prep without IEP context was generating low-quality generic content anyway.

**Files to change:**
- `web/src/features/children/components/child-detail-page.tsx` — remove the meeting prep section, its imports, and the `useMeetingPrep` hook call

### Item 2: Shorten Meeting Prep Checklist

Reduce from 6 sections to 3 focused sections:

| Current (6 sections) | New (3 sections) |
|---|---|
| QuestionsToAsk (3-8 items) | **QuestionsToAsk** (3-5 items) |
| RedFlagsToRaise (3-8 items) | **RedFlagsToRaise** (3-5 items) |
| DocumentsToBring (3-8 items) | **PreparationNotes** (2-3 items) — merged practical items |
| RightsToReference (3-8 items) | *(merged into PreparationNotes)* |
| GoalGaps (3-8 items) | *(removed — redundant with analysis goal analyses)* |
| GeneralTips (3-8 items) | *(removed — too generic)* |

**Total items: 8-13** (down from 18-48).

**Backend changes:**
- Update Claude system prompt in `MeetingPrepService.cs` — both `BuildModeAPrompt` and `BuildModeBPrompt` (though Mode B won't be called from UI anymore, keep it working for API compatibility)
- Update JSON schema in the prompt to request 3 sections instead of 6
- Add a `PreparationNotes` column to `MeetingPrepChecklist` entity (or reuse an existing column like `DocumentsToBring` and rename it via migration)

**Frontend changes:**
- Update `SECTIONS` array in `meeting-prep-tab.tsx` to show 3 sections
- Update progress bar calculation
- Update `checklist-section.tsx` icons/titles if needed

**Existing checklists:** Old 6-section checklists will still render correctly for their retained sections. The removed sections simply won't display. No data loss.

**Database approach:** Add a new `PreparationNotes` JSON column via migration. Keep the old columns (nullable) — they'll be null on new checklists but still populated on old ones. No destructive migration.

### Item 3 & 4: Auto-Polling for Async Operations

Create a shared `usePolling` hook and apply it to three async flows:

**Shared hook: `web/src/hooks/use-polling.ts`**

```typescript
function usePolling(
  fn: () => Promise<void>,
  intervalMs: number,
  enabled: boolean
): void
```

- Calls `fn` every `intervalMs` when `enabled` is true
- Pauses when `document.visibilityState === 'hidden'`
- Stops after 60 polls (5 minutes at 5s interval)
- Cleans up on unmount

**Apply to three flows:**

| Flow | Hook to modify | Poll when | Stop when |
|------|---------------|-----------|-----------|
| Document processing | `iep-viewer-page.tsx` | `document.status === 'processing'` | Status becomes `parsed` or `error` |
| IEP analysis | `use-iep-analysis.ts` | `analysis.status === 'analyzing' \|\| 'pending'` | Status becomes `completed` or `error` |
| Meeting prep | `use-meeting-prep.ts` | `checklist.status === 'generating' \|\| 'pending'` | Status becomes `completed` or `error` |

**Critical: IEP viewer sections reload.** When document polling detects a `processing → parsed` transition, the IEP viewer must also re-fetch sections (currently loaded once on mount). The polling callback should trigger both document reload and sections reload when the status changes to `parsed`.

### Item 5: IEP Analysis in Meeting Prep Context

**No changes needed.** Already implemented in `MeetingPrepService.BuildModeAPrompt`. When generating IEP-linked meeting prep, the service queries the most recent `IepAnalysis` for that document and includes:
- `analysis.OverallSummary`
- `analysis.OverallRedFlags`
- `analysis.GoalAnalyses`

### Item 6: Regenerate Meeting Prep

Add a "Regenerate" button to the meeting prep tab when a checklist is in `completed` state.

**Behavior:**
- Button appears in the `MeetingPrepTab` header area alongside the progress bar
- Clicking shows a confirmation: "This will create a new checklist. Your current progress will not be carried over. Continue?"
- On confirm, calls the existing `generateFromIep(iepId)` endpoint which creates a new checklist
- The `useMeetingPrep` hook already returns the most recent checklist by `createdAt`, so the new generating checklist surfaces automatically
- Previous checklist stays in the database (not deleted)

**Stale prep detection (bonus):** Similar to the existing `StaleAnalysisBanner` pattern for analysis, add a banner when the meeting prep was generated before the most recent analysis. Compare `checklist.createdAt` with `analysis.createdAt`. Show: "Your meeting prep was created before the latest analysis. Regenerate to include updated insights."

**Files to change:**
- `web/src/features/meeting-prep/components/meeting-prep-tab.tsx` — add regenerate button and confirmation
- `web/src/features/meeting-prep/hooks/use-meeting-prep.ts` — expose `generate` function for regenerate flow

## Acceptance Criteria

### Item 1: Move Meeting Prep
- [x] Meeting prep section removed from child detail page
- [x] No imports of meeting prep hooks/components remain in child-detail-page.tsx
- [x] Meeting prep tab on IEP viewer still works as before

### Item 2: Shorten Checklist
- [x] Claude prompt updated to request 3 sections: QuestionsToAsk, RedFlagsToRaise, PreparationNotes
- [x] QuestionsToAsk: 3-5 items
- [x] RedFlagsToRaise: 3-5 items
- [x] PreparationNotes: 2-3 items (documents, rights, practical prep)
- [x] Frontend SECTIONS array updated to 3 sections with appropriate icons
- [x] Progress bar calculates correctly with new sections
- [x] EF migration adds PreparationNotes column
- [x] Old 6-section checklists still render their existing data gracefully

### Item 3 & 4: Auto-Polling
- [x] Shared `usePolling` hook created at `web/src/hooks/use-polling.ts`
- [x] Polling pauses when browser tab is hidden
- [x] Polling stops after 5 minutes (60 polls at 5s)
- [x] Document processing: IEP viewer auto-updates when processing completes
- [x] Document processing: sections and tabs load automatically after `parsed` transition
- [x] IEP analysis: analysis tab auto-updates when analysis completes
- [x] Meeting prep: meeting prep tab auto-updates when generation completes

### Item 6: Regenerate
- [x] "Regenerate" button visible in completed state on meeting prep tab
- [x] Confirmation dialog warns about losing checked progress
- [x] New checklist is created and surfaces automatically
- [x] Stale prep banner shown when prep predates latest analysis

## Dependencies & Risks

- **Item 2 requires a DB migration** — adding `PreparationNotes` column. Low risk (additive, nullable column).
- **Item 3/4 polling may add load** — 5s interval per active user on async pages. Minimal given expected user count. Each poll is a single lightweight GET.
- **Item 2 prompt changes affect LLM output quality** — test with a few real IEPs to verify the 3-section format produces focused, actionable content.
- **Item 1 removes a feature path** — users with no parsed IEPs lose meeting prep access. Acceptable trade-off for content quality.

## Implementation Order

1. **Shared `usePolling` hook** — foundation for items 3, 4, and 6
2. **Auto-polling for document processing** (item 3) — fixes the most frustrating UX issue
3. **Auto-polling for analysis and meeting prep** (item 4) — same pattern, quick wins
4. **Remove meeting prep from child page** (item 1) — simple deletion
5. **Shorten checklist** (item 2) — backend prompt + frontend section changes + migration
6. **Regenerate button** (item 6) — depends on polling being in place so the regenerate flow doesn't create a new stuck spinner

## Sources & References

- Meeting prep hook: `web/src/features/meeting-prep/hooks/use-meeting-prep.ts`
- Meeting prep tab: `web/src/features/meeting-prep/components/meeting-prep-tab.tsx`
- Meeting prep service: `api/IepAssistant.Services/Implementations/MeetingPrepService.cs`
- Meeting prep entity: `api/IepAssistant.Domain/Entities/MeetingPrepChecklist.cs`
- IEP viewer page: `web/src/features/iep-documents/components/iep-viewer-page.tsx`
- IEP analysis hook: `web/src/features/iep-documents/hooks/use-iep-analysis.ts`
- Child detail page: `web/src/features/children/components/child-detail-page.tsx`
- Stale analysis banner pattern: `web/src/features/iep-documents/components/stale-analysis-banner.tsx`
