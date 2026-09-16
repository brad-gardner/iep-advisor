---
title: "feat: Record lifecycle — evaluation case, pre-meeting brief, attendance & decisions, signed artifacts, amendments, goal history, provider observations, district export"
type: feat
status: completed
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 7/8
depends_on: docs/plans/2026-09-15-006-feat-parent-draft-sharing-review-ai-questions-plan.md
---

# feat: Record lifecycle and system-of-record credibility (combined findings C03-review, C09-goals, C10, C11-progress slice)

## Problem Statement

As system of record, a finalized PDF is not a complete record. Missing: an evaluation case with referral/consent/clock; a pre-meeting brief for the LEA rep; attendance and structured decisions; signed-artifact handling; amendments that preserve the prior record; goals that persist across documents/years; provider progress observations; offline family participation; district data export; and OH-form-shaped PDF output.

## Decisions (defaults)

1. **Evaluation case** `EvaluationCase { SchoolStudentId, Kind (Initial|Reevaluation), ReferralDate, ReferralSource, ConsentRequestedAt, ConsentReceivedAt, ConsentDocumentBlob?, DeterminationDueDate (computed: consent + 60 calendar days OH default; editable with reason), Status (Open|ConsentPending|InProgress|Determined|Closed), EligibilityOutcome (Eligible|NotEligible|Withdrawn)?, DeterminationDate, DeterminationRationale, EtrDocumentInstanceId?, EtrAuthoredVersionId? }` + `EvaluatorAssignment { CaseId, UserId, Domain, DueDate, SubmittedAt, Notes }`. Plan 4's `ObligationService` gains the evaluation-clock obligation.
2. **Pre-meeting brief:** `GET /api/meetings/{id}/brief` — server-composed (AI-summarised, cached, regenerable) from the shared/draft revision: what's proposed, what changed vs. prior finalized version (semantic diff), resource-commitment items (new/changed services, placement, ESY, 1:1 — matched by semantics/keywords), procedural checklist (required participants present per type, notice sent ≥ N days, family input received), open family responses. Advisory language only.
3. **Attendance & decisions:** meeting detail gets `Attended` per participant (plan 4 field) and `MeetingDecision { MeetingId, TargetRowId?/FieldKey?, Text, Outcome (Agreed|Disagreed|Deferred), RecordedByUserId }` captured live; after the meeting, "Apply to draft" opens the editor with decisions listed as proposed edits (human applies).
4. **Signatures (print/sign):** finalize produces the PDF with signature blocks; `SignedArtifact { AuthoredDocumentVersionId, BlobUri, UploadedByUserId, UploadedAt, SignerSummary }` upload; `AuthoredDocumentVersion.SignatureStatus (Unsigned|PartiallySigned|Signed)`. Typed-name e-sign is explicitly **not** claimed; a `SignatureEvent` table exists only for future e-sign. Unsigned finalized documents appear on the school-admin home.
5. **Amendments:** `POST /api/authored-versions/{id}/amend` → new `DocumentInstance` prefilled from the version (all `_rowId`s preserved) with `AmendsVersionId`, `AmendmentReason`; finalizing yields a version with `AmendsVersionId` set and `EffectiveDate`; version list shows the chain; the PDF header states "Amendment to v{n}".
6. **Goals as entities:** `GoalRecord { SchoolStudentId, LineageId (= _rowId), AuthoredDocumentVersionId, Domain, GoalText, Baseline, TargetCriteria, MeasurementMethod, Timeframe, Status (Active|Met|NotMet|Retired|Carried), StatusReason, ReviewedAt }` projected on finalize (insert per goal row; prior records for the same lineage marked `Carried` when the row is carried, `Retired` when dropped with a required reason captured in the editor's "Remove goal" dialog). `GoalObservation { GoalRecordId, ObservedAt, Value (decimal?), Unit, Note, RecordedByUserId }` — the C11 provider slice: a 60-second "Log progress" form on the student page and from a notification link; trajectory sparkline on the goal (plan 5 chart primitive); "insufficient data" when < 2 points.
7. **Offline family participation:** `FamilyContactAttempt { SchoolStudentId, AttemptedAt, Method (Email|Phone|Letter|InPerson|Portal), Outcome, Note }` and `OfflineFamilyInput { SchoolStudentId, DocumentInstanceId?, ReceivedAt, Method, Summary, RecordedByUserId }` — recorded from the student page; included in brief and evidence bundle.
8. **District export:** `POST /api/district/exports` → background job building a ZIP: `manifest.json`, per-student folder with finalized PDFs, signed artifacts, `versions.json` (values), goals, meetings, contact attempts, responses (staff-visible only), audit extract; download via short-lived SAS; `ExportJob` row with status. Student-scoped export at `/api/educator/students/{id}/export`.
9. **OH PDF fidelity (structure-level):** `AuthoredDocumentPdfDocument` gains a form-style layout when the template `StateCode = "OH"`: ODE-style header (student, DOB, district, IEP dates, meeting date, form id "PR-07"/"PR-06", form version), numbered sections matching the seeded section titles, goals rendered as numbered blocks (not a grid), services table, participants block, signature blocks, page numbers, "Amendment" marking. Empty required sections print as "Not applicable / Not addressed" rather than being omitted. Pixel fidelity to the official form is out of scope; a school reviewer checks structure.

## Implementation Phases

### Phase 1 — Goals as entities + provider observations + retire reasons
- `GoalRecord`, `GoalObservation`, projection on finalize, remove-goal reason dialog, "Log progress" form + notification deep link, trajectory sparkline on student page and parent progress view, obligation for "no observation in 45 days" (owner: provider on team else lead).
- Tests: projection per finalize; lineage carry/retire; insufficient data rule.

### Phase 2 — Evaluation case + clock + evaluator assignments + ETR handoff
- Entities, service, endpoints, student page "Evaluation" card with timeline, consent capture (date + optional uploaded consent PDF), determination with rationale, "Create IEP from ETR" (prefill via plan 2 with evidence from the ETR version), `ObligationService` evaluation clock, not-eligible closure.
- Tests: clock computed from consent; overdue obligation; closure on not eligible; evaluator overdue escalation notification.

### Phase 3 — Meeting brief, attendance & decisions, apply-to-draft, offline participation
- Brief service (cached, regenerate), brief page (phone-readable), decisions capture on Held meetings, apply-to-draft list in editor, contact attempts + offline input forms and inclusion in brief/evidence.
- Tests: resource-commitment detection; procedural checklist per meeting type; decisions → proposed edits list.

### Phase 4 — Signatures, amendments, OH PDF layout, district export
- Signed artifact upload + status, amendment flow, PDF layout for OH templates + amendment header + signature blocks, export job + ZIP + download, student export.
- Tests: amendment chain; signature status transitions; PDF golden-structure test (section order, "Not addressed" rendering, headers); export manifest completeness; SAS expiry.

## Acceptance Criteria

- [x] Goals persist as records with lineage across finalizes/amendments; retiring a goal requires a reason; providers log observations in one form; trajectories render; "insufficient data" shown honestly.
  - Live: finalizing doc 5 projected `GoalRecord` 1 (Active); amending v2 → v3 carried the lineage (v2 record `Carried`, v3 `Active`) and the trajectory kept both observations (48, 55 wpm) across the carry; `POST /observations` with neither value nor note → 400; editor "Remove goal" dialog requires a reason and posts `goal-retirements` first (table-field tests); parent `/api/children/136/goals` returns the same read-only view.
- [x] Evaluation case tracks referral → consent → clock → determination with rationale; ETR findings prefill the IEP; not-eligible closes cleanly.
  - Live: create (Open) → consent requested (ConsentPending) → consent received Sep 10 (InProgress, determination due Nov 9 = +60 days OH) → evaluator assignment (overdue flag) → `EvaluationDetermination` obligation (Upcoming) on the student obligations list; NotEligible → Closed and create-iep prefill covered by EvaluationCaseServiceTests.
- [x] LEA rep gets a two-minute brief per meeting with resource commitments and a procedural checklist (advisory wording); attendance and per-item decisions are captured; decisions become proposed edits, never auto-applied.
  - Live: `POST /api/meetings/1/brief` composed from the shared revision (summary 1.3k chars, changes vs finalized, checklist: participants —, notice ✓, family input ✗, open responses 0); meeting 2 set Held → decision recorded → listed as a proposed edit on the amendment draft → mark-applied stamps `appliedAt`; brief page renders all blocks with the advisory disclaimer.
- [x] Offline family contact attempts and input are recordable and appear in the brief/evidence; school-only students complete and export without any family account.
  - Family contact card on the student page (log attempt / record offline input); items feed `StudentEvidenceService` and the brief; a school-only student exports without any family account (export tests).
- [x] Finalized versions carry signature status; signed PDFs can be attached; amendments create a linked version chain with reason and effective date.
  - Live: v3 `Unsigned` → multipart upload of a signed PDF with `signatureStatus=Signed` → `Signed`, 1 artifact, short-lived blob download URL; `POST /authored-versions/2/amend` → draft 6 with identical `_rowId`s and `amendsVersionId=2`, finalize → v3 with `amendsVersionNumber=2`, `effectiveDate=2026-10-01`, v2 `amendedByVersionIds=[3]`.
- [x] OH IEP/ETR PDFs render in form-style layout with headers, numbered sections, goal blocks, participants and signature blocks; empty sections print "Not addressed".
  - Live: v3 PDF (3 pages) — ODE PR-07 header with student/DOB/district/IEP date/meeting date/form version, amendment banner, numbered sections in template order, empty sections print "Not addressed", goal rendered as a numbered block, page x of y; structure locked by `AuthoredDocumentPdfDocumentTests` via the `Outline` seam (doubled numbering for already-numbered titles fixed after the visual check).
- [x] District and per-student export ZIPs contain PDFs, signed artifacts, values, goals, meetings, contact attempts, responses and an audit extract with a manifest.
  - Live: `POST /api/educator/students/21/export` → job Completed in ~10 s, 14 files / 223 KB: `manifest.json` (sha256 per file), `students/21/student.json`, `versions/IEP-v{1,2,3}.{pdf,json}`, `signed/…`, `goals.json`, `meetings.json`, `contact-attempts.json`, `offline-input.json`, `responses.json`, `audit.json` (51 rows); private parent notes verified absent; district list shows both scopes; download via 15-minute URL.
- [x] All checks pass.
  - `dotnet build` + `dotnet test` (809 passed); web `tsc -b`, `test:types`, vitest (451 passed), lint (36-error baseline unchanged), `build`, `guard:ux`.

## System-Wide Impact

- Finalize now runs a projection + signature status; wrap in the existing transaction.
- PDF worker must branch on template state; keep the generic layout for state-less templates.
- Export job must stream to blob (no in-memory ZIP > 100 MB).

## Dependencies & Risks

- Depends on plans 1–6.
- OH form structural fidelity needs a human reviewer — flagged as a follow-up gate in the pilot checklist (plan 8).

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) C03, C09, C10, C11; J3, J6, J7, J8; P6 Dennis brief
- Code: `AuthoredDocumentVersionService.cs`, `AuthoredDocumentPdfDocument.cs`, `IepVersionPdfDocument.cs`, `MeetingService` (plan 4), `StudentEvidenceService` (plan 2)

## Implementation Notes (2026-09-16)

- Branch `feat/record-lifecycle`; migrations `AddRecordLifecyclePhase12` and `AddRecordLifecyclePhase34` applied to QA (additive; `SignatureStatus` default corrected to `Unsigned` by hand in the scaffolded migration).
- Deviations: `MeetingStatus` has no `InProgress` (decisions on Held/Continued); notice-timing checklist keys on `NotificationKind.MeetingScheduled`; `unsignedFinalizedDocuments` for SchoolAdmin + CaseManager only; export `audit.json` resolves a fixed set of resource types (no `SchoolStudentId` on `AccessAuditLog`); export aggregate files are top-level (per the contract's literal layout); `ObligationDto` carries no per-record deep link, so home rows link to the student page; EF FKs from `EvaluationCase` to ETR instance/version are `Restrict` (SQL Server multiple-cascade-path rule).
- Coordinator fixes after live checks: instance detail exposes amendment fields; goal trajectories aggregate across the lineage; signed-artifact upload argument order; district export list includes student-scope jobs; OH section numbering.
- Known limits: digest attribution for provider-owned stale-goal obligations reaches leads only (providers see them on the student page); `GoalObservationRequested` notification kind exists but is not yet emitted; OH form fidelity is structural (human review is a plan-8 pilot gate); export buffers each member file in memory (archive itself streams through a FileStream).
- Operational validation: watch `ExportJobs` with `Status = Failed` and the `ExportWorker` log line "Building export"; `MeetingBriefs` generation 503s (Claude); `GoalObservations` volume after the pilot's first progress cycle. Mitigation: exports and briefs are additive features — disabling the nav entries hides them; no data path is destructive. Window: first month of a pilot; owner: platform admin.
- Subagents ran on Sonnet (Opus weekly limit); two workers stalled on the stream watchdog and were resumed in place.

