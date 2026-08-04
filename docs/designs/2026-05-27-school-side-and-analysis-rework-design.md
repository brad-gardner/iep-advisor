# Design Discussion: School-Side IEP Authoring, Analysis Rework & Student Workspace

**Date:** 2026-05-27
**Origin:** `docs/brainstorms/2026-05-26-school-side-and-analysis-rework-brainstorm.md`
**Feature:** Pivot the platform from parent-only PDF-analysis into a three-sided system (parent / educator / student) with a multi-doc `AnalysisRun`, structured school-side IEP authoring with immutable versions, separated meeting prep, and a student self-advocacy workspace — shipped in three vertical slices.

## Current State

The repo today (`/Users/bradgardner/dev/iep-assistant`):

- **Identity & access.** Single `User` table with `Role` string (`User` / `Admin`). `ChildProfile.UserId` is the sole owner FK (`api/IepAssistant.Domain/Entities/ChildProfile.cs`). `ChildAccess` (`Entities/ChildAccess.cs`) layers invites with `AccessRole { Viewer=0, Collaborator=1, Owner=2 }` and a SHA-hashed `InviteToken` flow (`Services/Implementations/ShareService.cs:35,109`). `IAccessService.HasMinimumRoleAsync` (`AccessService.cs:37`) gates almost every mutating service. `ChildProfile.UserId` is read in `ChildProfileRepository:21,28`, `SubscriptionService:120`, `AccountService:39`, `MeetingPrepService:301`.
- **Documents & analysis.** `IepDocument` and `EtrDocument` follow a two-step upload (metadata row → PDF upload via `POST /api/ieps/{id}/upload` with 50 MB cap + `%PDF-` magic-byte check in `IepDocumentsController.cs:93,105`). Both have parallel processing queues (`IepProcessingWorker`, `EtrProcessingWorker`) that send PDFs to Claude for sectioning. `IepAnalysis` / `EtrAnalysis` (`Entities/IepAnalysis.cs`, `EtrAnalysis.cs`) are **per-document** with JSON columns for `SectionAnalyses`, `GoalAnalyses`, `OverallRedFlags`, `SuggestedQuestions`, `AdvocacyGapAnalysis`, `ParentGoalsSnapshot`. Status is a `string` ("pending" / "analyzing" / "completed" / "error"); transitions live inline in services.
- **Goals.** `Goal` FKs `IepSection` only — no cross-version lineage, no direct child link.
- **Meeting Prep.** Already its own entity (`MeetingPrepChecklist`) and worker, but the analysis output also carries `SuggestedQuestions`, creating overlap.
- **AI plumbing.** `Anthropic.SDK` v5.10.0; each service `new`s its own `AnthropicClient` (`IepAnalysisService.cs:287`, five other call sites). No shared client/options abstraction. Prompts are inline verbatim strings.
- **Background work.** Channel-based queues + `BackgroundService` workers per doc type; workers use `IServiceScopeFactory`, services write status to DB.
- **PDF handling.** Only ingest. No PDF generation library is referenced in `IepAssistant.Services.csproj`. `IBlobStorageService` / `AzureBlobStorageService` handle blobs.
- **Migrations & conventions.** 23 EF migrations (`Domain/Data/Migrations/`). PascalCase columns, `*Configuration.cs` per entity, `IAuditableEntity` updated in `SaveChangesAsync` override, enums stored as strings via `HasConversion<string>()`. **No soft-delete query filter exists** — only `IsActive` flags used in repository `Where` clauses.
- **Frontend.** React 19 + React Router v7; routes in `web/src/app/routes.tsx`. Per-feature `api/*.ts` modules return `ApiResponse<T>` and pair with `hooks/*.ts` using `useState`/`useEffect` + `usePolling` (no react-query). Child detail uses nested routes + `ChildOutletContext`.

## Patterns to Follow

- **Background processing:** mirror `IepAnalysisQueue` + `IepAnalysisWorker` shape (`api/IepAssistant.Api/BackgroundServices/IepAnalysisWorker.cs`) for the new `AnalysisRunQueue`. One scope per item, status written by the service.
- **Service result wrapper:** every new service method returns `ServiceResult` / `ServiceResult<T>` (`Services/Models/ServiceResult.cs`).
- **Repository + EF configuration:** every new entity gets a sibling `*Configuration.cs` in `Domain/Data/Configurations/`, registered automatically via `ApplyConfigurationsFromAssembly`. Continue PascalCase columns, string-enum conversion, `IAuditableEntity` interface.
- **Access guard:** every new mutating/reading endpoint that touches a child- or student-scoped resource invokes `IAccessService.HasMinimumRoleAsync`. For the new educator/student actors, extend the access plane rather than parallel-implementing.
- **Two-step upload + metadata-first:** the existing pattern already supports "create the record before producing the artifact" — apply the same shape to the new `IepVersion` finalize flow (create version → generate PDF → attach).
- **Frontend feature folders:** new features go under `web/src/features/<feature>/{api,components,hooks,pages,types}`; reuse existing axios `api-client` and `usePolling` patterns.
- **Per-feature React hooks:** match `use-progress-reports.ts` shape — local state + `usePolling` for any async-status views (e.g., draft authoring saves, run status).

## Desired End State

**Domain model (added entities, in addition to existing):**

- `District`, `School`, `TeacherProfile`, `StudentProfile` (per-role profile side-tables; `User.Role` extended with `Educator` and `Student` discriminators).
- `SchoolStudent` (school-side child record); `ChildLink` (joins `SchoolStudent` ↔ parent `ChildProfile` by invite).
- `IepDraft` (mutable, school-side; aggregate root with mutable Sections, Goals, Services, Accommodations, Transition, Team).
- `IepVersion` (immutable snapshot; produced by finalize; owns its own copies of Section / Goal / Service / Accommodation / Transition / Team rows with a stable `LineageId` GUID per logical entity carried forward across versions).
- `IepVersionPdf` — generated PDF blob URI + checksum + render timestamp (one per `IepVersion`).
- `AnalysisRun` (child-scoped; selects N source documents — any combination of `IepDocument` / `EtrDocument` / `IepVersion` / `ProgressReport` — and produces sectioned-per-source + cross-doc synthesis JSON). Replaces per-doc `IepAnalysis` / `EtrAnalysis` as the primary analysis path; legacy rows migrate to single-source runs.
- `MeetingPrep` (graduated from `MeetingPrepChecklist`; child-scoped, links optionally to upcoming meeting date + selected docs; no longer overlapped with analysis `SuggestedQuestions`).
- `StudentWorkspace` (root entity owned by a `User{Role=Student}`); `StudentWorkspaceEntry` children with typed `EntryKind` (Strength, Interest, AccommodationRequest, MeetingStatement, AiInterviewAnswer). Pull-into-IEP and pull-into-MeetingPrep actions reference workspace entries.
- `AccessAuditLog` — append-only view/edit/share log on IEP-bearing resources (laying groundwork for FERPA-aligned access logging surfaced in domain research).

**Access plane changes:**
- `ChildAccess.UserId` and `ChildAccess.Role` keep working for parents.
- New `SchoolStudentAccess` (parallel to `ChildAccess`) governs educator/student access to `SchoolStudent`.
- `ChildProfile.UserId` is **kept** for now (changing every read site is risky); the multi-parent / co-parent capability is delivered by allowing multiple `ChildAccess { Role = Owner }` rows and treating ChildAccess as the authoritative access list. Reads that use `UserId` are updated to also include accepted Owner ChildAccess rows. (Avoids a high-risk rip-and-replace; see Open Questions.)

**Workflow:**
- Educator creates `IepDraft` under `SchoolStudent`. Live editing is **last-write-wins per field**, with `LastEditedByUserId` / `LastEditedAt` tracked per section so the UI can show "edited by X at T".
- "Finalize" creates an immutable `IepVersion` (full deep-copy of all children with `LineageId` carried forward), queues a PDF render job (QuestPDF), and — if the `SchoolStudent` is linked to a parent — makes the version visible to the parent and triggers an invite if no link exists.
- Parents see finalized `IepVersion`s alongside any legacy `IepDocument` uploads. Both feed the new `AnalysisRun` as selectable sources.
- Students with an invited account see a `StudentWorkspace` page; teachers and parents see "Pull from student workspace" actions in IEP authoring and Meeting Prep flows respectively.
- Educator AI: per-field inline assists ("rewrite goal", "suggest measurement criteria") + IEP-scoped sidebar chat. Same underlying Claude integration, refactored into a shared `IClaudeClient` abstraction so future prompt/transport changes are centralized.

**Frontend:**
- Parent surface adds a child-level "Analysis" tab (distinct from per-document Document/Analysis tabs) where users select sources and trigger `AnalysisRun`s.
- "Meeting Prep" promoted to its own child-level tab.
- New educator app shell at `/educator/*` (school-bound dashboard, student list, IEP authoring workspace, AI sidebar). Same `web` app; route guard by role.
- New student app shell at `/student/*` (single workspace page initially).

## Design Decisions

| Decision | Rationale |
| --- | --- |
| **QuestPDF** for `IepVersion` PDF generation. | Code-first DSL fits structured-data input; pure managed code, no Chromium dependency; Community MIT license likely sufficient now. (See research.) |
| **Separate `IepDraft` + `IepVersion` aggregates** (not temporal tables, not JSON snapshot). | Versions are queryable, diff-friendly, and naturally back the PDF generator. Temporal tables don't model the *finalize* event cleanly. |
| **`LineageId` (GUID) on every versioned child entity** (Goal, Service, Section, Accommodation, TransitionItem). | Industry-standard "same logical entity across versions" pattern. Powers year-over-year analysis and progress-report alignment. |
| **`AnalysisRun` replaces `IepAnalysis`/`EtrAnalysis` as the primary path; legacy rows migrate to single-source runs.** | Brainstorm decision; backfill keeps a unified analyses timeline. |
| **Single `User` table with role discriminator + per-role profile side-tables.** | Brainstorm decision; one auth pipeline; profiles isolate per-role data. |
| **Reuse `ChildAccess` for co-parent (multiple `Owner` rows); keep `ChildProfile.UserId` for now.** | Avoids a 7-file rip of `UserId` reads. The multi-owner outcome is achieved through the existing access plane. Tag for revisit if `UserId` reads grow. |
| **Educator/student access via parallel `SchoolStudentAccess`**, not by overloading `ChildAccess`. | Keeps semantics clean: `ChildAccess` = parent-side; `SchoolStudentAccess` = school-side. The `ChildLink` joins them. |
| **Last-write-wins editing, with `LastEditedBy*` per section.** | Brainstorm decision. Cheapest first ship; can layer locks or CRDT later. |
| **`IClaudeClient` abstraction introduced in Slice 1.** | Eliminates the 6× `new AnthropicClient(...)` copies; sets up centralized retry/telemetry/prompt builders we'll need for AI inline assists in Slice 2. |
| **`AccessAuditLog` added in Slice 2 (school-side launch).** | FERPA-aligned audit logging is industry-standard and cheap to add when school-side ships; not needed for parent-only Slice 1. |
| **Feature flags via simple `Feature:` config keys** read at service entry (`IFeatureFlags`). | Vertical-slice rollout requires runtime toggle without redeploys per slice. Defer richer flagging until needed. |
| **Slice 3 student workspace** is its own surface, not a tab inside the IEP. | Brainstorm decision: decouple identity/consent complexity from document mechanics. |

## Slice-Level Scope

**Slice 1 — Analysis rework + Meeting Prep extraction (parent-side first).**
Deliverables: `AnalysisRun` entity + queue + worker, child-level "Analysis" tab, child-level "Meeting Prep" tab, removal of overlapping `SuggestedQuestions` from analysis output, `IClaudeClient` abstraction, backfill migration of `IepAnalysis` / `EtrAnalysis` → `AnalysisRun`. Feature-flagged. No school/educator/student surfaces yet.

**Slice 2 — School-side authoring + sharing.**
Deliverables: `District`/`School`/`TeacherProfile`/`SchoolStudent`/`ChildLink` entities; `Educator` role; educator app shell; IEP authoring UI (all sections + Goals + Services + Accommodations + Transition); `IepDraft` → `IepVersion` finalize with QuestPDF generation; inline AI assists on fields + IEP-scoped sidebar chat; parent-side visibility of `IepVersion`s; school-initiated parent-invite flow; `AccessAuditLog` added. Feature-flagged.

**Slice 3 — Student self-advocacy workspace.**
Deliverables: `Student` role + invite flow (parent- or teacher-initiated); `StudentWorkspace` + entries; student app shell; pull-into-IEP and pull-into-MeetingPrep affordances. Feature-flagged.

## Testing Strategy

- **Unit tests** — new services (`AnalysisRunService`, `IepDraftService`, `IepVersionService`, `StudentWorkspaceService`), `LineageId` carry-forward on finalize (specifically: add / carry / drop goal cases), `IClaudeClient` adapter (mocked HTTP).
- **Integration tests** — finalize-then-edit-draft path (draft remains mutable, version stays immutable, verified via `SaveChanges` interceptor); legacy-analysis backfill migration is idempotent; multi-owner `ChildAccess` correctly grants both parents read+share; `SchoolStudentAccess` grants only intended scope.
- **Migration tests** — sample staging DB before/after; verify legacy `IepAnalysis`/`EtrAnalysis` rows visible as `AnalysisRun`s; row counts match.
- **Manual browser verification** — at each slice: golden-path flow end-to-end (Slice 1: upload IEP+ETR → run combined analysis → see sectioned output; Slice 2: educator creates draft → finalizes → parent sees version; Slice 3: parent invites student → student adds entries → teacher pulls into goal). Document caveats per `CLAUDE.md` guidance ("type checking verifies code, not features").
- **AI assertions** — Claude-backed paths use deterministic prompt-stamp assertions where possible (input shape → expected output schema), not exact-string matches.

## Open Questions

1. **`ChildProfile.UserId` deprecation timeline.** We're keeping it for Slice 1 to avoid a wide refactor. Should Slice 2 retire it (replace all reads with `ChildAccess`-driven queries), or keep it indefinitely as a denormalized "primary owner" pointer? Recommendation: keep + add migration that backfills an Owner `ChildAccess` row for every existing `UserId`, then prefer `ChildAccess` for new reads.
2. **Multi-tenant data isolation for districts.** Today everything is row-level via `UserId` / `ChildAccess`. When District/School lands, do educators in District A ever need a query that scans across schools, or is every educator query implicitly bounded by their `SchoolId`? Recommendation: enforce `SchoolId`-bound queries in repositories.
3. **PDF rendering trigger.** Synchronous on finalize, or queued? QuestPDF is fast (sub-second for documents this size), but if finalize also triggers an `AnalysisRun`, both should probably go through the queue. Recommendation: queue PDF render; surface "rendering…" state in UI.
4. **Audit log retention & query surface.** Append-only is easy; do we need a parent-facing access log view in Slice 2 or just admin? Recommendation: store from Slice 2, parent-facing UI deferred.
5. **State / jurisdiction.** Domain research surfaces that transition age (14 vs. 16), age-of-majority transfer, and IEP form templates vary by state. This rework doesn't introduce per-state form rendering, but Slice 2 should at minimum capture `ChildProfile.StateCode` and `SchoolStudent.StateCode` so future per-state logic has a hook. Confirm we don't try to ship per-state forms now.
6. **Section 504 plans** — domain research flags these as a sibling document type. Out of scope for this rework but worth a placeholder in the IEP authoring data model (e.g., `IepVersion.DocumentType` enum: `Iep | Section504` so the same aggregate could later host both)?
7. **E-signature / consent.** Industry standard on finalize; out of scope for this rework, but we should leave a finalize-time hook (no-op now) so it's not a schema change later. Confirm deferral.
8. **Versioning semantics for ETRs.** ETRs in real-world workflow are produced on a triennial cycle; should `EtrDocument` also gain a draft/version model in Slice 2, or are ETRs school-uploaded PDFs forever? Recommendation: defer — ETR authoring is a much larger effort and rarely a parent-facing pain point.

---

**Next:** I'll wait for your review of this design before producing the structure outline and writing the plan. Specifically interested in your take on the open questions and on whether the slice boundaries match how you want to ship.
