---
title: "feat: Deliberate whole-draft sharing, parent review with AI explanation and private questions, per-item responses, converge view, post-meeting summary"
type: feat
status: completed
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 6/8
depends_on: docs/plans/2026-09-15-005-feat-role-homes-operational-dashboards-plan.md
---

# feat: Parent draft loop (combined findings C04)

## Problem Statement

A document instance is Draft → Finalizing → Finalized; nothing between "hidden" and "final". Parents can now (plan 1) open finalized versions, but they cannot see a draft before the meeting, get a plain-language explanation of it, ask questions privately, or respond to specific goals. `AnalysisSourceType` covers only parent-uploaded PDFs. This is the flagship J4 journey and the demo's centrepiece.

## Decisions (defaults)

1. **Shared revision = immutable snapshot.** `SharedDraftRevision { DocumentInstanceId, RevisionNumber, ValuesJson (frozen), DocumentTemplateVersionId, SharedByUserId, SharedAt, Message, Status (Active|Superseded|Withdrawn), ChangeSummaryJson? }`. Sharing is one deliberate action with a recipient preview (linked parents + student account) and an optional note; internal edits after sharing are private until re-shared; re-share computes a semantic diff vs. the previous revision (`ChangeSummary`: added/removed/changed rows by `_rowId`, changed narrative fields).
2. **District policy:** `District.FamilyDraftSharingEnabled` (default true). When false the share action is hidden and the endpoint refuses; finalized versions still visible per existing behaviour.
3. **Explanations and Q&A run on the exact revision**, never on a re-uploaded PDF: new `AnalysisSourceType.SharedDraftRevision` feeding a **new lightweight path**, not the heavy `AnalysisRun` pipeline: `DraftExplanationService` produces per-section and per-goal plain-language explanations (cached per revision in `SharedDraftExplanation`), and `DraftQuestionService.AskAsync` answers a parent's private question over the revision + the parent's own evidence (child profile, contributions, uploaded prior IEP sections) with citations. Parent questions/answers are **private** (`ParentDraftNote`, never visible to staff) unless the parent explicitly submits one as a response.
4. **Responses:** `DraftResponse { SharedDraftRevisionId, ParentUserId, TargetRowId? / TargetFieldKey?, Kind (Agree|Question|ChangeRequest|Comment), Text, CreatedAt, Status (Open|Resolved), ResolvedByUserId, ResolvedAt, StaffReply }`. Parent sees own responses; staff (Collaborator+) see all; resolving requires a reply or a "resolved in draft" flag.
5. **Converge view** for staff on the instance: list of open responses grouped by item, jump-to-field, reply/resolve, "what changed since last share" summary, and a "Share again" CTA. Also feeds plan 5's home sections.
6. **Post-meeting family summary**: after a meeting is marked Held (plan 4) and the document finalized, staff can generate (AI draft, human edits, explicit send) a plain-language `MeetingSummary` sent to the family as a notification/email with a link — reuse notification pipeline.
7. **Acknowledgement:** parent "I've reviewed this revision" stamp (`DraftAcknowledgement`) — not consent, not signature; labelled as such.

## Proposed Solution

### Backend
- Entities + migrations: `SharedDraftRevision`, `SharedDraftExplanation`, `ParentDraftNote`, `DraftResponse`, `DraftAcknowledgement`, `MeetingSummary`, `District.FamilyDraftSharingEnabled`.
- `DraftSharingService`: `PreviewRecipients`, `Share(instanceId, message)`, `Withdraw`, `ListForInstance`, `GetForParent(childId, revisionId)`, `ChangeSummary(prev, next)` (semantic diff using `_rowId`).
- `DraftExplanationService` (Claude; cached; parent-advocate framing; cites revision fields), `DraftQuestionService` (ephemeral answer, persisted as private note), `DraftResponseService`, `MeetingSummaryService`.
- Endpoints: `POST /api/documents/{id}/share`, `GET /api/documents/{id}/shares`, `POST /api/documents/{id}/shares/{rev}/withdraw`, `GET /api/documents/{id}/responses`, `POST /responses/{id}/resolve`; parent: `GET /api/children/{childId}/shared-drafts`, `GET /shared-drafts/{rev}`, `GET /shared-drafts/{rev}/explanations`, `POST /shared-drafts/{rev}/ask`, `GET/POST/DELETE /shared-drafts/{rev}/notes`, `POST /shared-drafts/{rev}/responses`, `POST /shared-drafts/{rev}/acknowledge`; `POST /api/meetings/{id}/summary/draft`, `PUT`, `POST /send`.
- Notifications: `DraftShared`, `DraftResponseReceived`, `DraftResponseResolved`, `MeetingSummarySent` (plan 4 kinds).
- Access: parent must hold an accepted active `ChildLink` to the student; staff Collaborator+; withdrawn/superseded revisions remain readable to parent (history) but marked.

### Web
- Staff: editor header "Share with family" button → recipient preview modal (note, policy notice) → shared banner with revision number/date and "Changes since last share"; Converge tab on the document page (responses, reply/resolve); meeting summary composer on the meeting detail after Held.
- Parent: `/children/:childId/shared-drafts/:rev` — phone-first reading view: section list, each goal/service row as a card with "Explain" (cached explanation) and "Ask a question" (private thread, labelled *Private — only you can see this*), "Respond" (Agree / Question / Request change — labelled *Sent to the school team*); revision switcher with change highlights; "Mark as reviewed". Home/overview cards from plan 5 light up.

## Implementation Phases

### Phase 1 — Share revision + parent reading view + acknowledgement
- Entities, sharing service, policy flag, staff share UI, parent reading view, notifications. Tests: snapshot immutability; edits after share invisible; withdraw; revoked link 404; policy off refuses.
- Checkpoint: share, edit a goal, parent still sees the shared text; re-share shows the change.

### Phase 2 — Explanations + private Q&A
- Explanation service with caching; question service with parent evidence; private notes UI. Tests: explanations cite field keys; questions never leak staff-only data; notes invisible to staff endpoints; prompt-injection in draft text stays in data tags.
- Checkpoint: parent taps Explain on a goal and asks "is this ambitious enough?" and gets a cited answer.

### Phase 3 — Responses + converge + post-meeting summary
- Response service/UI both sides, converge view, home sections wired, meeting summary compose/send. Tests: resolve requires reply/flag; response visibility; summary send creates notification/email.
- Checkpoint: parent submits a question on goal 2; case manager replies and resolves; after the meeting the family receives a summary.

## Acceptance Criteria

- [x] Sharing is a deliberate action with recipient preview; the parent sees a frozen revision; later edits stay private until re-shared; re-share shows what changed.
  - Live: `GET /share/preview` listed the parent recipient; `POST /share` created rev 1, editing the reading goal and re-sharing produced rev 2 with `changeSummary` "1 row changed." and rev 1 → Superseded; parent detail returns the frozen `values`.
- [x] District policy can disable family draft sharing.
  - `PUT /api/district { familyDraftSharingEnabled }` + toggle on `/educator/admin/schools`; share returns 403 when disabled (DraftSharingServiceTests).
- [x] Parent gets per-section/per-goal plain-language explanations and can ask private questions answered with citations to the revision; private notes are never visible to staff.
  - Live: `GET /shared-drafts/1/explanations` generated once and served from `SharedDraftExplanation` on the second call; `POST /ask` returned an answer and persisted a `ParentDraftNote`; staff `GET /notes` → 403.
- [x] Parent can submit Agree/Question/Change-request per item; staff see, reply, resolve in a converge view; counts appear on both homes.
  - Live: parent `POST /shared-drafts/2/responses` → team `ResponseReceived`; staff resolve with reply → parent `DraftResponseResolved` at `/children/136/shared-drafts/2`; resolve without reply/flag → 400; `GET /converge` groups open/resolved; home counts wired in `HomeService` (HomeServiceTests).
- [x] Parent can mark a revision reviewed (not consent); staff see the stamp.
  - Live: `POST /acknowledge` stamps `acknowledgedAt` (idempotent); staff `GET /shares` shows `acknowledgements[]`; UI label "This is not consent or a signature".
- [x] Post-meeting summary can be AI-drafted, edited, and explicitly sent; family receives it.
  - `POST /meetings/{id}/summary/draft` → `PUT` edit → `POST /send` (MeetingSummaryServiceTests); family route `/children/:childId/meetings/:meetingId/summary`; `MeetingSummarySent` notification.
- [x] School-only students (no family link) are unaffected — no blocking states, no nag.
  - Preview for a student with no accepted `ChildLink` returns an empty recipient list and share returns 400 with no UI nag; the Share button only renders when `policyEnabled`.
- [x] All checks pass.
  - `dotnet build` + `dotnet test` (762 passed); web `tsc -b`, `test:types`, vitest (407 passed), lint (36-error baseline unchanged), `build`, `guard:ux`.

## System-Wide Impact

- Usage metering: explanations/questions consume Claude — record `UsageRecord` rows under the district (not the parent's subscription) with a new `UsageKind`; parent subscription paywall must not trigger for school-sponsored sharing.
- Audit: `Share`, `View` (parent revision), `Export` actions recorded via `AuditLogger`.

## Dependencies & Risks

- Depends on plans 1–2 (semantics, `_rowId`, evidence) and 4 (notifications).
- Prompt cost: cache explanations per revision.

## Sources

- Origin: [combined-findings](../gap/combined-findings.md) C04; J4 visibility model (whole-draft, deliberate); P1 privacy boundary; P7 veto rules
- Code: `AuthoredDocumentVersionService.cs`, `AnalysisRunService.cs` (prompt guards), `ParentVersionDetailPage`, `ChildLinkService.cs`

## Implementation Notes (2026-09-16)

- Branch `feat/parent-draft-review`; migration `20260916072345_AddFamilyDraftSharing` applied to the QA database (six new tables, `Districts.FamilyDraftSharingEnabled`, `UsageRecords.DistrictId` + nullable `ChildProfileId`).
- Deviations from the contract: the meeting summary DTO is named `FamilyMeetingSummaryDto` (a `MeetingSummaryDto` already existed); scalar field citations use `[F:{fieldKey}]` (no row id); the district toggle lives on the schools admin page rather than a separate settings page.
- Review-stage fix during work: notification links now use the revision **id** and the child-scoped parent route (`/children/{childId}/shared-drafts/{revisionId}`); the resolve notification resolves the child id through `ParentAccessResolver`.
- Known QA-data limitation: engagement counters on the staff home read 0 until parents on the QA tenant respond; explanation generation on the QA draft produced 2 sections and 0 item explanations because the draft's goal rows had no populated text at generation time.
- Operational validation: watch `UsageRecords` rows with `OperationType in ('draft_explanation','draft_question','meeting_summary')` and 503s from `/explanations`; mitigation is the district toggle (no data loss — revisions persist). Window: first two weeks of a pilot; owner: platform admin.
- Subagents ran on Sonnet because of the Opus weekly limit; independent review follows in `/sht-review`.

