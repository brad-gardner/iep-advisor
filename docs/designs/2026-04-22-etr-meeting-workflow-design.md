# Design Discussion: ETR Meeting Workflow

**Date:** 2026-04-22
**Feature:** Parallel workflow for Evaluation Team Reports (ETRs), mirroring the IEP document workflow — upload, parse, analyze, meeting-prep.
**Origin brainstorm:** `docs/brainstorms/2026-04-22-etr-meeting-workflow-brainstorm.md`

## Current State

The IEP workflow is mature and proven. It's a three-entity document pipeline wired through two background channels:

- **Entities:** `IepDocument`, `IepSection`, `IepAnalysis` — `api/IepAssistant.Domain/Entities/`
- **Storage:** Azure Blob + `IBlobStorageService`; metadata in `IepDocuments` table
- **Processing:** `IepProcessingQueue` (singleton `Channel<int>`) → `IepProcessingWorker` → `IepProcessingService.ProcessDocumentAsync` (`api/IepAssistant.Services/Implementations/IepProcessingService.cs:44`); Claude `claude-sonnet-4-20250514` called inline at lines 190–237
- **Analysis (manually triggered):** `POST /api/ieps/{id}/analyze` → `IepAnalysisQueue` → `IepAnalysisWorker` → `IepAnalysisService.AnalyzeWithClaudeAsync` (`IepAnalysisService.cs:277`)
- **Meeting prep:** `MeetingPrepChecklist` links to `ChildProfile` (required) + optional `IepDocument`; generated via `MeetingPrepQueue` / `MeetingPrepWorker`
- **Controllers:** `IepDocumentsController` enforces class-level `[Authorize]`; explicit `AccessService.HasMinimumRoleAsync(…, Collaborator, …)` checks on `/process` and `/analyze`; other ownership via service-layer `GetByIdAsync(id, userId)`
- **EF Core:** Fluent API via `IEntityTypeConfiguration<T>` in `api/IepAssistant.Domain/Data/Configurations/`; migrations in `Data/Migrations/`; generic `IRepository<T>` with specialized repos extending it
- **Frontend:** `web/src/features/iep-documents/` — axios + hand-rolled hooks (no react-query); routes in `web/src/app/routes.tsx`; create-IEP entry point in `web/src/features/children/components/child-detail-page.tsx:268`
- **DI:** Queues + workers registered in `api/IepAssistant.Api/Program.cs:57-72`

**No `DocumentType` enum or polymorphic document abstraction exists.** `MeetingType` on `IepDocument` is a free-form `string?`. `IepDocument` is domain-specific in name and fields.

## Patterns to Follow

- **Entity shape:** Mirror `IepDocument` triplet precisely. `EtrDocument`, `EtrSection`, `EtrAnalysis` — each with same audit/soft-delete approach as IEP (IsActive on EtrDocument only; audit via `IAuditableEntity`).
- **Configuration:** One `EtrDocumentConfiguration` / `EtrSectionConfiguration` / `EtrAnalysisConfiguration` file in `Data/Configurations/` — fluent API, HasMaxLength, HasIndex on FKs, Cascade delete from `ChildProfile`.
- **Queues + Workers:** Copy `IepProcessingQueue`/`IepProcessingWorker` pattern verbatim → `EtrProcessingQueue`/`EtrProcessingWorker`. Same for analysis. Register in `Program.cs` next to IEP workers.
- **Service layer:** `IEtrProcessingService` and `IEtrAnalysisService` with the same method shapes (`ProcessDocumentAsync(int id)`, `AnalyzeDocumentAsync(int id)`). Claude calls use the same named `HttpClient "Claude"`.
- **Controllers:** `EtrDocumentsController` — same route surface as `IepDocumentsController`, class-level `[Authorize]`, subscription gates on upload/analyze, `AccessService` role checks on destructive/paid operations.
- **Meeting prep:** Add optional `EtrDocumentId` FK to existing `MeetingPrepChecklist` entity. Add a parallel controller endpoint `POST /api/etrs/{etrId}/meeting-prep` in `MeetingPrepController`. `MeetingPrepService` gets a branch for ETR context.
- **Frontend structure:** New `web/src/features/etr-documents/` mirroring `iep-documents/` folder. Axios + hand-rolled hooks; no introduction of react-query (consistency over ergonomics).
- **Routes:** `/etrs`, `/etrs/:id`, plus per-child listing surfaced from both the child detail page and a new top-level ETR page.
- **Navigation:** Add top-level "ETRs" entry to the primary nav (per brainstorm decision).
- **Create entry points:** Two — (a) child detail page gets a "New ETR" button next to the existing "New IEP" button; (b) top-level ETR page has a global create button that prompts for child selection.

## Desired End State

A parent can:

1. From a child's detail page, click **"New ETR"**, enter meeting date + draft/final + optional notes, and see an ETR shell.
2. Upload a PDF (≤50MB). Background worker parses it via Claude into ETR-specific sections and persists.
3. Trigger analysis manually (or auto — see Open Questions). Background worker produces analysis across **four pillars**: assessment completeness, eligibility determination review, red flags & parent rights, meeting questions.
4. View the ETR in a dedicated viewer with tabs for Sections, Analysis (with subviews per pillar), and Meeting Prep.
5. Generate a meeting prep checklist anchored to the ETR.
6. See all ETRs for a child in a timeline view; no forced single-active constraint.
7. Access a **top-level ETR page** showing their ETRs across children (per brainstorm decision).

Goals and the `Goal` entity remain IEP-only — ETRs don't define goals.

## Design Decisions

### Carried forward from brainstorm
- **Separate parallel entities** — `EtrDocument`, `EtrSection`, `EtrAnalysis` (not extending `IepDocument`, not a polymorphic base). See brainstorm: Key Decisions.
- **Separate top-level ETR section** in navigation. See brainstorm: Key Decisions.
- **Full parity with IEP** on v1 scope (upload → parse → analysis → meeting prep). See brainstorm.
- **Multiple ETRs per child** with chronological timeline; no auto draft-vs-prior comparison in v1. See brainstorm.
- **Four analysis pillars:** assessment completeness, eligibility review, red flags, meeting questions. See brainstorm.

### New technical decisions
- **ETR document status field** reuses IEP values: `created, uploaded, processing, parsed, error`. Consistency > ETR-specific vocabulary.
- **ETR draft/final distinction** stored as a separate `DocumentState` string field on `EtrDocument` (`draft | final`), independent of processing `Status`. Free-form string (HasMaxLength 20), consistent with `MeetingType` pattern on IEP — not a DB enum, no lookup table (no runtime editability needed).
- **ETR section taxonomy** (initial set, emitted by Claude parser): `referral_reason`, `background`, `parent_input`, `assessments_administered`, `cognitive`, `academic`, `behavioral_social_emotional`, `adaptive`, `communication`, `motor_sensory`, `health_vision_hearing`, `eligibility_determination`, `team_recommendations`, `other`. String-typed (not enum) matching `IepSection.SectionType` convention.
- **MeetingType analog for ETR** = `EvaluationType` field on `EtrDocument`: `initial | reevaluation | transfer | other`. Free-form string.
- **Meeting prep wiring** — extend `MeetingPrepChecklist` with nullable `EtrDocumentId` FK (additive migration, no breaking change). Service dispatches to an ETR-specific prompt path when `EtrDocumentId` is set. Chose this over a separate `EtrMeetingPrepChecklist` entity because the checklist structure (questions, documents, rights, notes) is the same shape — only the generation prompt changes.
- **Claude prompts** live inline in `EtrProcessingService` and `EtrAnalysisService`, matching the current IEP pattern. No prompt-file abstraction introduced. (Flagged for later refactor if prompt drift becomes a maintenance issue.)
- **Model + token limits** mirror IEP (`claude-sonnet-4-20250514`, MaxTokens 16384 for parsing; analysis uses existing allocation).
- **PDF size limit** 50MB, same as IEP. Subscription gating mirrors IEP upload/analyze.
- **Frontend state** — reuse axios + hand-rolled hooks pattern. Do NOT introduce react-query in this feature.
- **Routes:** `/etrs` (top-level list), `/etrs/:id` (viewer), plus backend endpoints `GET/POST /api/children/{childId}/etrs`, `GET/PUT/DELETE /api/etrs/{id}`, `POST /api/etrs/{id}/upload`, `POST /api/etrs/{id}/process`, `POST /api/etrs/{id}/analyze`, `GET /api/etrs/{id}/sections`, `GET /api/etrs/{id}/analysis`, `GET /api/etrs/{id}/download`, `POST /api/etrs/{id}/meeting-prep`. Plus `GET /api/etrs` for top-level cross-child listing.
- **Authorization:** Class-level `[Authorize]` + service-layer `GetByIdAsync(id, userId)` ownership filtering via `AccessService`; explicit `Collaborator` check on `/process` and `/analyze`. Copies IEP pattern.
- **Access via `ChildProfile`** (same as IEP). No direct `UserId` on `EtrDocument`.
- **Data Design Decisions (enum-like fields):**
  - `EtrDocument.Status` → string (mirrors IEP, code-only concept, no deploy-free editing needed)
  - `EtrDocument.DocumentState` (draft/final) → string, not a lookup table
  - `EtrDocument.EvaluationType` → string, not a lookup table
  - `EtrSection.SectionType` → string, not a lookup table (Claude outputs these values; no FK needed)
  - `EtrAnalysis.Status` → string (mirrors IEP)
  - Decision in all cases: **code-level values**, not DB lookup tables. Rationale: these are structural to the app, not business-configurable, and changing them should require code review + deploy.

### Entity shape (ERD)

```mermaid
erDiagram
    ChildProfile ||--o{ EtrDocument : "has many"
    EtrDocument ||--o{ EtrSection : "parses into"
    EtrDocument ||--|| EtrAnalysis : "has one"
    EtrDocument ||--o{ MeetingPrepChecklist : "may anchor"

    EtrDocument {
        int Id PK
        int ChildProfileId FK
        string FileName
        string BlobUri
        DateTime UploadDate
        DateTime EvaluationDate nullable
        string EvaluationType nullable
        string DocumentState
        string Notes nullable
        string Status
        long FileSizeBytes
        bool IsActive
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    EtrSection {
        int Id PK
        int EtrDocumentId FK
        string SectionType
        string RawText nullable
        string ParsedContent nullable
        int DisplayOrder
    }
    EtrAnalysis {
        int Id PK
        int EtrDocumentId FK
        string Status
        string AssessmentCompleteness nullable
        string EligibilityReview nullable
        string OverallRedFlags nullable
        string SuggestedQuestions nullable
        string OverallSummary nullable
        string ErrorMessage nullable
    }
```

## Open Questions

1. **Auto-trigger analysis after parsing, or keep it manual like IEP?** IEP requires a user click on `/analyze` after parsing completes. ETR meetings are often last-minute ("meeting tomorrow"), so auto-trigger on `parsed` status might be more parent-friendly. Tradeoff: cost + surprise if user didn't want it. **Recommendation: manual for v1 to match IEP, revisit after feedback.**

2. **Top-level ETR page scope:** Show ETRs across all children, or require child selection first? Most parents have one child; a flat list is simpler. **Recommendation: flat cross-child list grouped by child, matching how the IEP section would work if it existed at top level.**

3. **Analysis reruns:** Should parents be able to reanalyze an ETR after uploading a newer draft to the same ETR record, or should every upload create a new ETR? IEP currently allows re-upload + re-analyze. **Recommendation: each ETR is one uploaded document — uploading a newer draft creates a new ETR entry. Simpler mental model and matches the "timeline" view.**

4. **Meeting prep prompt divergence:** How different should the ETR meeting-prep prompt be from the IEP one? ETR meetings have distinct rights (e.g., challenging eligibility determination, requesting IEE — Independent Educational Evaluation). **Recommendation: dedicated ETR prompt branch in `MeetingPrepService`, not a shared prompt with a variable.**

5. **Subscription metering:** Should ETR analysis count against the same per-child analysis limit as IEP, a separate ETR limit, or a combined document-analysis limit? **Recommendation: separate ETR analysis limit mirroring IEP — avoids IEP users' limits being consumed by one-time ETR analyses.**

## Testing Strategy

- **Backend unit tests:** Service-layer tests for `EtrProcessingService` (Claude response parsing, error paths, status transitions) and `EtrAnalysisService`. Mock Claude HttpClient.
- **Backend integration tests:** Controller-level tests for auth, child-ownership, subscription gates, and full create→upload→parse→analyze flow using in-memory queue + real DbContext on SQLite.
- **Frontend:** Component tests for create-etr-form (validation), etr-upload (file rejection), analysis-tab rendering each pillar. No new testing framework — use the existing `/web` test setup.
- **E2E:** One Playwright flow: parent creates ETR, uploads sample PDF, waits for parse, triggers analysis, generates meeting prep. Extends existing e2e suite.
- **Manual verification:** Upload a real-world sample ETR (draft + final, covering an initial eligibility and a reevaluation) and confirm section parsing + analysis pillar coverage.
- **Migration safety:** `MeetingPrepChecklist.EtrDocumentId` added as nullable FK — additive, zero-downtime.
