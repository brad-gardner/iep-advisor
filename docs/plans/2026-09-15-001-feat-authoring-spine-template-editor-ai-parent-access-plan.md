---
title: "feat: Authoring spine — semantic template blocks, AI assist + student input in the template editor, OH IEP/ETR/504 templates, parent access to authored versions"
type: feat
status: completed
date: 2026-09-15
origin: docs/gap/combined-findings.md
slicing_approach: vertical
series: school-sale-readiness 1/8
---

# feat: Authoring spine (combined findings C01, C03-seed, C04-parent-view, hygiene)

## Problem Statement

`IEP_AUTHORING_MODE = 'template'` (`web/src/config/features.ts`) makes the template-driven document engine the only authoring path a school sees, and in that path:

- **No AI.** `IepAssistController` / `IepAssistService` bind to the legacy `IepDraft` aggregate. `document-authoring/` has no assist, chat, or pull-from-student.
- **Goals have no identity.** The seeded template models goals as a `Table` of `Text` columns; rows are positional arrays with no persisted id, so nothing downstream (AI, parent responses, progress, amendments) can address a goal.
- **ETR and 504 dead-end.** Only a state-less "Default IEP" template is seeded; choosing ETR or 504 in the New-document dialog returns "No document template is available."
- **Parents can't see finalized template documents.** `GET /api/children/{childId}/authored-versions` exists but `SchoolIepsCard` fetches legacy `/iep-versions` only; there is no parent route for `AuthoredDocumentVersion`.
- Two authoring concepts ("IEP versions" legacy and "Documents" template) sit side by side on `/educator/students/:id`.

## Decisions (defaults chosen for this run)

1. **Template engine stays the single authoring engine.** The legacy typed editor is retired from navigation in this plan and its routes removed after parity; legacy `IepVersion` data stays readable (versions list on student page and parent card keep working for existing rows).
2. **Semantic config, not new FieldTypes.** Add an optional `semantic` key to field config (`"studentProfile" | "presentLevels" | "goals" | "services" | "accommodations" | "transition" | "eligibility" | "placement" | ...`) and an optional `semantic` on Table columns (`"domain" | "goalText" | "baseline" | "targetCriteria" | "measurementMethod" | "timeframe" | "serviceType" | "frequency" | ...`). Validator accepts/normalizes them; AI, prefill and projections resolve fields by semantic, never by GUID.
3. **Stable row identity.** Every Table row carries a server-assigned `_rowId` (GUID) persisted inside the row object in `ValuesJson`. `CoerceTable` preserves/assigns it; the client passes it through. This is the goal/service/accommodation lineage key used by plans 2, 6 and 7.
4. **Launch state = Ohio.** Seed `StateCode = "OH"` templates for IEP (PR-07 section set), ETR (PR-06 section set) and a minimal Section 504 template (state-less). Form version pinned in template `Name` ("Ohio IEP (PR-07, 2026-27)"). Full PDF form fidelity is plan 7; this plan gets the *structure* right.
5. **AI assist targets become template-addressed:** `(instanceId, fieldKey, rowId?)` instead of `(draftId, goalId)`.

## Proposed Solution

### Data / config

- `TemplateFieldConfigValidator`: accept `semantic` (string, from a closed allow-list) on field config and on table columns; reject unknown values; preserve on round-trip. Add `FieldSemantic` / `ColumnSemantic` static classes with the allow-lists in `IepAssistant.Domain` (or Services/Models).
- `DocumentInstanceService.CoerceTable`: read `_rowId` from incoming rows; if missing/invalid, assign `Guid.NewGuid()`; always emit it. Ignore `_rowId` as a column. `MaxValuesJsonBytes` unchanged.
- `DefaultIepTemplateSeeder` → generalize into `TemplateSeeder` that seeds: default IEP (unchanged keys, **now with semantics**), `OH` IEP, `OH` ETR, default 504. Idempotent per `(StateCode, DocumentTypeId)`. Existing "Default IEP" published version gets a **new published version** (v2) carrying semantics; instances pinned to v1 keep rendering.
- `DocumentTemplateService`/admin template builder: expose `semantic` selectors (field-level and column-level) so admins can tag custom templates.

### AI in the template editor

- New `DocumentAssistController` + `IDocumentAssistService`:
  - `POST /api/documents/{instanceId}/assist` body `{ fieldKey, rowId?, kind: Rewrite|Improve|SuggestMeasurement }` → `AssistResultModel` (suggestion text + rationale). Resolves the field's semantic to pick the goal/section/service prompt; builds context from the instance's semantic fields (student profile, present levels, goals).
  - `POST /api/documents/{instanceId}/chat` body `{ messages[] }` → ephemeral chat with a compact rendering of the instance values (semantic-labelled).
  - Reuse `IepAssistService`'s prompts/guards (move prompt constants into `AssistPrompts` shared static class). Same access gate (`CanActOnStudentAsync(Collaborator)`), same audit action.
- Web: port `assist/assist-popover`, `assist-kind-menu`, `assist-suggestion-panel`, `chat/*`, `use-field-assist`, `use-iep-chat` into `document-authoring/components/assist/` and `chat/` parameterised by `(instanceId, fieldKey, rowId)`. Rich-text and Text renderers get an "AI help" affordance; Table rows with a `goalText`/`serviceType` semantic get a per-row "AI help". "Ask the assistant" drawer on the editor header.
- Pull-from-student: `GET /api/educator/students/{id}/shareable-entries` already exists; port `pull-from-student/*` into the goals table row and present-levels field.

### Editor UX

- Section navigator (sticky left rail / tabs on narrow) built from template sections; scroll-spy; keyboard `[`/`]`.
- Save status pill (reuse `AutosaveIndicator`), read-only banner unchanged.
- Advisory completeness sidebar: required-empty fields, goals missing baseline/measurement, services missing frequency — computed client-side from semantics; never blocks finalize (finalize already validates required fields server-side — keep).

### Parent access

- `web/src/features/iep-versions/components/school-ieps-card.tsx` → also load `/api/children/{childId}/authored-versions`; render one merged list ("School documents") with type badge and finalized date.
- New route `/children/:childId/authored-versions/:versionId` → `ParentAuthoredVersionPage` rendering `AuthoredVersionSnapshot` + PDF download (existing `authored-pdf-download`). Access check already in `AuthoredDocumentVersionService` (child link).

### Retire legacy paths

- Remove `IEP_AUTHORING_MODE`, the typed-mode block on `educator-student-detail-page.tsx`, routes `/educator/students/:id/iep-drafts*`, and `iep-authoring/` components once assist/chat/pull are ported (keep `hooks/use-autosave*`, `use-flush-*`, `save-status` — move to `document-authoring/hooks` or `@/hooks`). Keep `IepAssistController`/`IepDraftService` API for now (Phase 3 deletes controller + web API module; backend service and entities stay until plan 7's migration review).
- Student detail: single "Documents" card listing drafts + finalized (template) and, only if any exist, a collapsed "Legacy IEP versions" list.

### Hygiene in scope

- `web/vite.config.ts` proxy target → `https://localhost:7200`; `launchSettings.json` applicationUrl → 7200; runbook note.
- CI: add `test` jobs to `deploy-api.yml` (`dotnet test`) and `deploy-web.yml` (`npm ci && npm run type-check && npm test && npm run build`) that gate the deploy jobs (`needs:`).

## Implementation Phases

### Phase 1 — Semantics + row identity + seeds (backend, testable via services)
- Validator allow-lists; `CoerceTable` `_rowId`; `TemplateSeeder` (default IEP v2 with semantics, OH IEP, OH ETR, default 504); hosted seeder runs all.
- Tests: validator round-trips semantics; `_rowId` assigned/preserved; seeder idempotent, creates 4 templates, ETR/504 resolve.
- Checkpoint: New-document dialog creates ETR and 504 without error.

### Phase 2 — Document assist + chat (API + web)
- `DocumentAssistService`, controller, DTOs; prompt sharing; audit.
- Web assist popover/suggestion panel/chat drawer wired to text, rich text, table rows; pull-from-student.
- Tests: service tests (access gate, semantic resolution, unavailable Claude → friendly failure); web unit tests for assist hook.
- Checkpoint: in the editor, "AI help" on a goal row returns a suggestion that can be accepted into the row.

### Phase 3 — Editor UX + parent access + retire legacy + hygiene
- Section navigator, completeness sidebar, merged parent card + parent authored-version route, remove legacy web routes/flag, student page single Documents card, vite/launch ports, CI test gates.
- Tests: web tests for navigator + parent card merge; e2e spec update if `iep-documents.spec` references legacy drafts.
- Checkpoint: educator finalizes an OH IEP; linked parent opens it from the child overview and downloads the PDF.

## Acceptance Criteria

- [x] Creating IEP, ETR and Section 504 documents succeeds for an OH district and for a state-less district (default fallback), with no "No document template" error. *(Live: OH ETR created for a student with no explicit state — resolution now falls back school → district.)*
- [x] Every Table row saved through `PUT /api/documents/{id}/values` carries a stable `_rowId` that survives edits, reorders and finalize.
- [x] Template fields/columns can declare `semantic`; seeded templates declare them; the admin builder can set them.
- [x] In the template editor a case manager can request Rewrite / Improve / SuggestMeasurement on a goal row, a service row, and a narrative section; accept applies the suggestion; the assistant chat answers using the document's content; pull-from-student inserts a shared entry. All gated by Collaborator access and audited. *(Live: Rewrite on a goal row returned a measurable goal grounded in the 42 wpm baseline; Accept persisted it.)*
- [x] Editor has a section navigator and an advisory completeness panel; finalize is never blocked by advisory items.
- [x] Parent child-overview lists finalized template documents; parent can open a version page and download its PDF; unrelated/revoked parent gets 403/404. *(Access rules verified by `AuthoredDocumentVersionServiceTests`; UI error state verified live for an unrelated parent.)*
- [x] `IEP_AUTHORING_MODE` and the legacy typed editor routes/components are gone; existing legacy finalized versions remain viewable.
- [x] Vite proxy/launch settings use 7200; CI runs backend and web tests before deploy.
- [x] `dotnet test` (504), `npm run type-check`, `npm test` (105), `npm run build`, `npm run guard:ux` pass. `npm run lint`: 37 pre-existing `react-hooks/set-state-in-effect` errors remain (was 38 on main; none introduced).

## Implementation notes (2026-09-15)

- Semantic row blocks (goals/services/accommodations/transition/participants/evaluator reports) render as **cards** with labelled inputs rather than a grid — a six-column goal table did not fit the editor column and trapped the suggestion panel inside the horizontal scroller.
- Row identity: the client adopts server-assigned `_rowId`s by position after each save (`adoptRowIds`) so subsequent saves echo the same id instead of minting a new one.
- QA database: startup seeder published Default IEP **v2** (semantic) and created OH IEP (v3), OH ETR (v4), Default 504 (v5) template versions. No EF migration was needed for this plan.
- Legacy backend (`IepDraft*`, `IepAssistController`, `IepVersionController` finalize) remains for data compatibility; only the web surface was removed.

## Operational validation notes (for ship)

- **Runtime impact:** new endpoints `POST /api/documents/{id}/assist|chat` call Claude (1–2k max tokens). Watch `DocumentAssistService` error logs ("Document assist … failed with {Kind}") and 503 rates on those routes for the first day.
- **Healthy signal:** startup log lines "Default IEP template seed finished: AlreadySeeded" and "Template catalog seed finished: created [], skipped [3 names]" after the first boot.
- **Failure signal / mitigation:** a burst of `RateLimited`/`Configuration` assist failures → check `Anthropic:ApiKey`/model config; the UI degrades to "AI help is unavailable right now" and never blocks saving. Rollback: revert the web deploy (API is additive).
- **Validation window:** first educator session after deploy — create an OH IEP, run one assist, finalize, open as linked parent. Owner: Brad.

## System-Wide Impact

- **Interaction graph:** finalize → `AuthoredDocumentVersionService` snapshot (values incl. `_rowId`) → PDF worker. PDF renderer must ignore `_rowId` when rendering tables.
- **Error propagation:** assist failures map to `UnavailableMessage` (existing `ClaudeApiException` classification from PR #22) — reuse.
- **State lifecycle:** template v2 publish must not orphan v1 instances; `TemplateResolutionService` picks highest published — new instances get v2, old keep v1.
- **API surface parity:** admin template DTOs + `template-config.ts` parse/serialize must carry `semantic`.
- **Integration scenarios:** OH district + default district resolution; assist on a row whose semantic is missing (fallback to generic section prompt); parent with revoked link.

## Dependencies & Risks

- Anthropic key present in QA (`Anthropic:ApiKey`) — assist tests use the fake client.
- Legacy e2e specs may reference `/iep-drafts` — update.
- QuestPDF table rendering must skip `_rowId`.

## Sources

- Origin: [docs/gap/combined-findings.md](../gap/combined-findings.md) C01, C03, C04, §6 hygiene
- Evidence: [docs/gap/claude-findings.md](../gap/claude-findings.md) F1
- Code: `web/src/config/features.ts`, `web/src/features/document-authoring/*`, `web/src/features/iep-authoring/*`, `api/IepAssistant.Services/Implementations/{DocumentInstanceService,TemplateFieldConfigValidator,DefaultIepTemplateSeeder,IepAssistService,AuthoredDocumentPdfDocument}.cs`, `api/IepAssistant.Api/Controllers/{IepAssistController,AuthoredDocumentVersionController}.cs`
- Ohio forms: https://education.ohio.gov/Topics/Special-Education/Federal-and-State-Requirements/Ohio-Required-and-Optional-Forms-Updated (PR-07 IEP, PR-06 ETR; 2026-27 versions)
