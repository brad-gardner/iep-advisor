---
module: "RecordLifecycle"
date: "2026-09-16"
problem_type: "security_issue"
component: "service_object"
symptoms:
  - "A signed-artifact upload stored the client's multipart file name verbatim and later interpolated it into a ZIP entry path in district exports (Zip Slip)"
  - "Consent and signed-artifact uploads trusted the declared Content-Type — an executable declared as application/pdf was accepted and re-served as a PDF"
  - "Pressing Escape, clicking the backdrop or the × while a confirmed request was in flight closed the dialog but the request still landed — a goal row was removed behind a 'cancel'"
  - "After an amendment carried a goal into a new record, the student page showed the goal with zero observations and 'Insufficient data'"
  - "A district export ran three queries per student; the parent home linked shared drafts by revision number"
root_cause: "missing_validation"
dotnet_version: "9.0"
resolution_type: "code_fix"
severity: "high"
tags: [zip-slip, path-traversal, magic-bytes, file-upload, modal, prevent-close, race, lineage, goal-records, export, batching, react-router, code-review]
---

# Record lifecycle: upload guards belong in one place, a confirmed request outlives a dismissed dialog, and progress follows the goal's lineage

## Problem
Plan 7 (`docs/plans/2026-09-15-007-…`) turned the finalized PDF into a real record: goal records with
lineage and provider observations, an evaluation case with a consent clock, a pre-meeting brief with
decisions that become proposed edits, signed artifacts and amendments, offline family input, district
and per-student export, and an OH form-style PDF. Four workers (two .NET, two React) built ~28k lines;
gates were green and the flows were live-verified before review. Five review passes still found
two P1s and nineteen P2s — mostly the same handful of classes, each worth keeping.

## What went wrong

1. **Upload guards re-derived per endpoint.** Three older upload controllers each checked the `%PDF-`
   magic bytes and ran `Path.GetFileName` on the client's name. The two new upload paths (consent PDF,
   signed artifact) lived in services and did neither: they compared the *declared* Content-Type (and
   skipped even that when the header was blank) and stored the raw multipart file name. The export
   worker then built `signed/{id}-{FileName}` ZIP entries from it — a Zip Slip for whoever extracts a
   district export. Two independent reviewers found it (P1 / P2).
2. **"Cancel" that only disabled one button.** Every new dialog disabled its footer Cancel while
   submitting — but Esc, the backdrop and the header × all went straight to `onClose`. The
   remove-goal dialog awaited the retirement POST, and its closure then removed the row and autosaved
   regardless of the dismissal. The same shape existed in three more dialogs and in the shared
   `ConfirmDialog` (~20 call sites).
3. **Progress attached to the record, not the goal.** `GoalObservation` hung off the `GoalRecord`
   a finalize produced; carrying the goal into an amendment created a new record — with no history.
   Found only by the live check (finalize → observe → amend → finalize), not by the unit tests, which
   never exercised two finalizes with an observation in between.
4. **Per-row work in a per-district job.** The export walked students one at a time with three
   queries each, next to an aggregate method in the same file that already batched with `Contains`.
5. Smaller, same family: a fresh amendment draft had no `RowVersion` (concurrency off until the first
   save); a swapped `(versionId, userId)` argument pair made every signed-artifact upload 404; the
   doubled "2. Section 1:" numbering in the OH PDF; a regenerate that could overwrite another meeting's
   brief after a route change; a focus effect keyed on an array that re-fired on every update.

## Solution

- **`PdfUploadGuard`** (`api/IepAssistant.Services/Implementations/PdfUploadGuard.cs`): `LooksLikePdfAsync`
  (reads and rewinds the magic header; refuses non-seekable streams) and `SafeFileName` (bare name,
  separators/`..`/invalid chars stripped *until stable*, bounded to the 260-char column keeping the
  extension, fallback name). Applied before the blob write in both services and again when the export
  builds the ZIP entry, so legacy rows are re-sanitised on read. Tests: `PdfUploadGuardTests`,
  `SignedArtifactUpload_ChecksTheBytesNotTheHeader_AndStoresABareFileName`,
  `ReceiveConsent_ChecksTheDocumentBytes_AndStoresABareFileName`.
- **`Modal.preventClose` / `Drawer.preventClose`** (`web/src/components/ui/{modal,drawer}.tsx`): one prop
  that makes Esc, backdrop and × inert (× disabled). `ConfirmDialog` forwards `loading` to it, so every
  confirm gets the guard; every dialog that owns its submitting flag passes it (plan-7 forms, plan-6
  ask/respond/resolve/share, finalize, new-document, schedule-meeting, create-template). Hosts whose flag
  lives in a child form are a recorded follow-up. `table-field` keeps a second line of
  defence — the resolved retirement only removes the row if it is still the pending target.
  Test: `ignores Esc / backdrop / × while the retirement request is in flight…`.
- **Lineage-wide trajectories** (`GoalRecordService.LoadCurrentAsync`): one query over the
  observations of every lineage in view (`(SchoolStudentId, LineageId)`), so a carried goal keeps its
  history. Test: `GetForStudentAsync_CarriedGoal_KeepsTheLineagesObservationsOnItsTrajectory`.
- **Batched export prefetch** (`ExportService.PrefetchStudentDataAsync`): students, versions and
  artifacts in three queries per 200-student chunk, looked up per student while streaming the archive.
- Also: `RowVersion` seeded on amend; controller argument order fixed and the whole controller set
  audited; `ParentAccessResolver` is now the single linked-parent read rule across three services;
  stale guards on the brief regenerate (`currentMeetingRef`, a render-time reset of the generate state
  on a meeting switch, and a per-call token so only the newest request applies) and a
  request-generation counter on export polling; deep-link focus
  runs once per goal id; download failures surface in a `role="alert"` Notice; OH section titles
  that already carry "Section N:" are not re-numbered.

## Why this addresses the root cause
Each defect was a rule that existed somewhere in the codebase but not on the new path: the upload
checks lived in controllers, the "don't act on a cancelled dialog" rule lived in one Cancel button, the
"progress belongs to the goal" rule lived in the plan text, the batching pattern lived one method
down. The fixes move each rule to the one place every path has to go through (a shared guard, the
Modal primitive, the goal query, the prefetch), and each has a test that walks the second path.

## Verification
- `dotnet build IepAssistant.sln && dotnet test IepAssistant.Services.Tests` → **821 passed**.
- `npx tsc -b --noEmit && npm run test:types && npx vitest run` → **458 passed**; `npm run lint` at the
  36-error baseline; `npm run build`; `npm run guard:ux`.
- Live QA (API on 7200, student 21): goal projection, observations, amend → finalize chain with the
  trajectory carried; evaluation case with the +60-day clock; brief; decisions → proposed edits →
  mark-applied; signed artifact upload → `Signed` → download; student export ZIP (14 files, manifest
  with sha256, private notes absent, 51 audit rows); OH PDF page 1 inspected visually.
- Review: 8 configured reviewers (Sonnet). Pass 1 → 2 P1 / 10 P2 / 5 P3; pass 2 → 4 P2; pass 3 → 4 P2;
  pass 4 → 1 P2; pass 5 (targeted at the six-line round-4 diff) → recorded in `todos/REVIEWED`.
- Limitations: the district-scale export (hundreds of students) is verified by query shape and a
  single-student run, not a load test; OH form fidelity is structural only (pilot gate in plan 8).

## Prevention
- Any client-supplied file name or bytes go through `PdfUploadGuard` (or a sibling guard for other
  types) — never `Path.GetFileName` inline, never a Content-Type comparison alone.
- A dialog whose confirm awaits a request passes its submitting flag as `preventClose` (Modal and
  Drawer); `ConfirmDialog` does this for you via `loading`. Handlers that mutate after an `await`
  re-check the target, and a re-triggerable request carries a per-call token.
- When an entity is projected per finalize, decide which facts belong to the *lineage* (observations,
  history) and query them by lineage; write the two-finalize test first.
- In a job that walks N rows, batch the per-row queries by chunk; the aggregate method next door is
  the template.
- When a worker reports "verified", still live-run the flow that crosses two of its features
  (finalize → observe → amend) — the argument swap and the lineage gap were both only visible there.

## Related
- Plan: `docs/plans/2026-09-15-007-feat-record-lifecycle-signatures-amendments-goal-history-export-plan.md`
- Findings: `todos/113–134` (P3 follow-up: `todos/125-pending-p3-record-lifecycle.md`)
- Prior learning on hidden-editor reveal and prompt guards: `docs/solutions/logic-errors/2026-09-16-family-draft-sharing-…`
