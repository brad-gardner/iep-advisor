---
title: "feat: IEP authoring — read-first sections, focused goal/service editors, item owners"
type: feat
status: active
date: 2026-10-02
design: docs/designs/2026-10-02-iep-authoring-read-edit-sections-design.md
mockup: docs/designs/2026-10-02-iep-authoring-read-edit-sections-mockup.html
slicing_approach: vertical
---

# feat: IEP authoring — read-first sections, focused goal/service editors, item owners

## Overview

Rework the district IEP authoring page (`/educator/documents/:instanceId`, Edit tab) so it uses the screen for editing:

- completeness moves to a strip under the header;
- the right rail goes and the assistant chat becomes a drawer;
- sections render read-only with a per-section **Edit → Done / Discard changes** toggle, and edits autosave while a section is open;
- Goals get compact cards and a focused editor with **objectives/benchmarks**;
- Services get a schedule view and a structured editor;
- Goals, Services, Accommodations and Transition items each get a **team-member owner**.

(see design: docs/designs/2026-10-02-iep-authoring-read-edit-sections-design.md, approved 2026-10-02; mockup: docs/designs/2026-10-02-iep-authoring-read-edit-sections-mockup.html)

## Problem Statement

- At 1080p the 3-column grid (`document-editor.tsx:191-197`, `14rem | 1fr | 16rem` plus a 22rem chat column) leaves the editing column about 690px wide, with a mostly empty completeness rail.
- Every field is always an open editor, so the page is a wall of toolbars rather than a readable IEP.
- Goals and Services are the most complex parts of an IEP, yet their prose cells are 2-row `<textarea>`s (`table-field.tsx:513-528`). Goals have no objectives/benchmarks.
- Nothing records who is responsible for a goal, service, accommodation or transition item.

## Proposed Solution

Decided in the design (see design doc):

1. **Page frame:**
   - header, then a completeness strip (percent, bar, required/advisory counts, "Show items" with jump links);
   - proposed edits;
   - a 2-column layout (sticky section navigator plus a wide content column, up to about 1100px at 1080p);
   - assistant chat in a right `Drawer`.
2. **Read-first sections:**
   - each section card renders formatted read-only content plus **Edit**;
   - edit mode shows the field editors and a header with the save state, **Discard changes** and **Done**;
   - several sections can be open at once;
   - empty sections show "Not started — Start editing".
3. **Saving:**
   - unchanged single path: `useAutosave` → `PUT /api/documents/{id}/values` with `rowVersion`;
   - edits autosave while open (existing debounce plus an idle flush);
   - **Done** flushes and closes;
   - **Discard changes** restores the snapshot taken at Edit and saves it, confirming if autosaves already landed;
   - 409 handling unchanged.
4. **Owners:**
   - reserved row cell `_ownerUserId` on rows of the `goals`, `services`, `accommodations` and `transition` semantics;
   - must be an active member of the student's team (server-validated);
   - carried forward by prefill;
   - the editor shows the name; the family draft and PDF show the **role** only ("Responsible: Intervention specialist");
   - `GoalRecord.OwnerUserId` is set at finalize.
5. **Goals:**
   - read: cards (number, area, clamped statement, measurement and report chips, objectives count, owner, carried/keep badges);
   - edit: a full-width focused editor with rich-text goal statement, baseline and target, measurement and reporting, timeframe, owner (side panel with evidence), and an ordered **objectives** list `_objectives: [{ _rowId, description, criteria, targetDate }]` with server-assigned ids.
6. **Services:**
   - read: a schedule table (service and provider role, frequency × minutes, setting, dates, owner) and a total minutes/week in the header;
   - edit: an inline structured editor (count + per week/month, minutes per session, setting, start/end, notes, owner) that writes **normalized text** into the template's existing `frequency` / `duration` / `location` columns (e.g. "2 per week", "30 minutes"), so the PDF, completeness and AI stay unchanged.
7. **Accommodations / Transition:** owner picker per row; larger auto-growing prose fields; read view grouped by category or goal area.
8. **Completeness:** new advisory checks ("goal/service/accommodation/transition item has no owner", "goal has no objectives"), mirrored client (`lib/completeness.ts`) and server (`DocumentCompletenessService`). The strip shows "updating…" while any section has unsaved edits.

## Design direction

- **Mode:** operate. Educators read an IEP top to bottom, open one part to change it, and trust it saved.
- **Visual system:** preserve. Tokens in `web/tailwind.config.js` (`brand-teal`/`slate`/`amber`/`danger`, Lora serif headings, DM Sans body, `rounded-card`/`button`/`input`/`badge`); components in `web/src/components/ui` (`Card`, `Badge`, `Button`, `Notice`, `Drawer`, `RichTextEditor`).
- **Screens and states:**
  - *Completeness strip:* 0 flags ("Nothing flagged"); required + advisory; expanded item list; "updating…" while edits are unsaved.
  - *Section card:* read (with content); read (empty, "Not started"); edit (idle / saving / saved / save error / conflict 409 notice); Discard confirmation.
  - *Goals:* list read; no goals (empty, "Add goal"); goal editor (new / existing / carried-forward needing Keep); objectives empty / several; remove-goal-with-reason dialog; owner unset (amber hint).
  - *Services:* schedule read; empty; inline editor; owner unset.
  - *Accommodations / Transition:* grouped read; editor rows with owner.
  - *Chat drawer:* open / closed.
  - *Responsive:* ≥1280 two columns; below `lg` the navigator becomes horizontal chips (existing behavior).
- **Follow:** the mockup frame and spacing; the existing `SectionNavigator`, `ProposedEditsPanel`, `FieldAssistBar`, `RemoveGoalDialog`; `AutosaveIndicator` semantics for the save state.
- **Avoid:**
  - modals for editing (inline only);
  - new colors or icon sets;
  - helper text lighter than `slate-500`;
  - dense toolbars in read mode;
  - personal names on family-facing outputs;
  - unbounded line length in read mode (about 75ch max for prose).
- **Assumptions:**
  - "E" keyboard shortcut toggles edit on the active section (next to the existing `[`/`]`);
  - Edit moves focus to the first field and Done returns focus to the Edit button;
  - the idle flush interval is 5s.
- **Tooling:** impeccable 4.1.1 (`/Users/bradgardner/.claude/skills/impeccable`): craft floor, detector, audit.

## Technical Approach

### Architecture

- **No new tables for document content.** Owners and objectives live in `DocumentInstance.ValuesJson` table rows as reserved keys, next to `_rowId` / `_carriedFrom` / `_confirmed`:
  - `DocumentSemantics.cs` (`RowMetaKeys`) and `web/src/features/admin/templates/document-semantics.ts` gain `_ownerUserId` and `_objectives`.
  - Server row normalization in `DocumentInstanceService.cs` (~400-470, which drops unknown meta keys and assigns `_rowId`):
    - keeps `_ownerUserId` only for the four block semantics and only when the user is an active `StudentTeamMember` of the instance's student (otherwise it drops the key and returns a field-level warning);
    - normalizes `_objectives` to an array (max 20), assigning objective `_rowId`s with the same dedupe rules.
- **One migration:** `GoalRecord.OwnerUserId int? NULL` (+ FK to Users, `ON DELETE SET NULL`); `GoalRecordService.ProjectOnFinalizeAsync` copies it.
- **Prefill:** `DocumentPrefillService` already copies row cells and stamps `_carriedFrom`; verify `_ownerUserId` and `_objectives` are carried (objective ids re-issued), and drop owners no longer on the team.
- **Read renderers:** a new `field-renderers/read/*` set (`ReadRichText` renders markdown, `ReadText`, `ReadDate`, `ReadSelect`, `ReadCheckbox`, `ReadTable` generic) plus semantic read views `GoalCardList`, `ServiceSchedule`, `AccommodationGroups`, `TransitionList`.
- **Section edit state:**
  - `useSectionEditing(instanceId)` holds the open set, Edit snapshots (values for the section's field keys) and the per-section save status derived from the field autosaves registered under that section via `useFlushRegistry`;
  - **Done** runs `flushSection(sectionId)`;
  - **Discard** writes the snapshot back through `saveValues` (one PUT) and closes;
  - an idle flush interval runs only while sections are open.
- **Rich-text cells in block rows:** goal statement, baseline, target, transition services and accommodation text use `RichTextEditor` within the row editor; values are stored as markdown strings in the same cell (the PDF already renders markdown for RichText; verify table cells do too, else extend `AuthoredDocumentPdfDocument`).
- **Owner picker:** `TeamMemberSelect` loads `GET /api/educator/students/{studentId}/team` once per page (cached in the editor context) and shows name + role; "Unassigned" option.
- **Layout:** `document-editor.tsx` grid becomes `lg:grid-cols-[13rem_minmax(0,1fr)]` with a max-width container; `CompletenessPanel` becomes `CompletenessStrip`; `ChatPanel` moves into `Drawer`.

### Implementation Phases

#### Phase 1: Page frame + read-first sections with Edit / Done / Discard

- [x] `CompletenessStrip` replaces `CompletenessPanel` (jump links reuse `lib/section-dom.ts` `jumpToField`/`jumpToSection`; "updating…" flag from the section editing state)
- [x] `document-editor.tsx` two-column layout with max width; chat in `Drawer`; header save state
- [x] Read renderers for every field type, plus a generic table read view (the semantic views come in later phases)
- [x] `useSectionEditing` + section card header (Edit / Saving / Saved / Discard / Done); multiple open sections; "E" shortcut; focus management
- [x] Discard: snapshot at Edit, confirm when autosaves landed, single `saveValues` restore
- [x] Idle flush (5s) while sections are open; flush-on-navigate unchanged; Finalize flushes all and closes open sections
- [x] Tests (Vitest): read render per field type; Edit/Done/Discard; multi-open; flush on Done; Discard restore + confirm; completeness strip states + jump; chat drawer; keyboard/focus. A first `document-editor.test.tsx` integration test (none exists today).

**Checkpoint:** at 1920×1080 the page matches the mockup frame; editing a narrative section autosaves, Done closes it, and Discard restores it; reload shows saved content in read mode.

#### Phase 2: Owners on goals, services, accommodations, transition

- [x] `RowMetaKeys.OwnerUserId` (C# + TS); normalization keeps it only for the four semantics when the owner is an active team member; warning otherwise
- [x] `TeamMemberSelect` + editor-context team cache; owner shown in the read views (name + role) and in row editors
- [x] Accommodations and Transition row editors: auto-growing rich-text prose fields + owner; read views grouped (category / goal area)
- [x] Prefill carries owners (drop if no longer on the team)
- [x] Migration `AddGoalRecordOwner`; `GoalRecordService` copies the owner at finalize
- [x] Family draft rendering + `AuthoredDocumentPdfDocument`: "Responsible: <role>" (role from the team member's `TeamRole`), never the name
- [x] Completeness advisory "has no owner" (client + server, parity test)
- [x] Tests: normalization (team member kept, non-member dropped, wrong semantic dropped); prefill carry; GoalRecord owner; PDF/draft role-only; completeness parity; picker UI

**Checkpoint:** assign owners in all four sections; reload; create an amendment and confirm the owners carried forward; the PDF shows the role only.

#### Phase 3: Goals rework

- [x] `GoalCardList` read view (clamped statement, chips, objectives count, owner, carried/keep badges)
- [x] `GoalEditor` focused editor (rich-text statement/baseline/target, measurement, reporting, timeframe, owner + evidence side panel, Remove with the existing reason dialog, Discard/Done scoped to the goal)
- [x] `_objectives` (C# normalization + TS types): add/remove/reorder with immutable React keys, ids adopted from the exact sent array (row-identity rules in `docs/solutions/ui-bugs/2026-09-15-autosaved-list-rows-*.md`)
- [x] Completeness advisory "goal has no objectives"; AI help per goal field unchanged; "Pull from student" unchanged
- [x] Objectives in the family draft and PDF goal block; `GoalRecord` snapshot includes objectives (JSON)
- [x] Tests: objectives identity (focus retained during autosave, reorder, delete), normalization ids, PDF objectives, completeness

**Checkpoint:** create a goal with 3 objectives, reorder them, reload, finalize, and see them on the PDF; the goal owner is on the `GoalRecord`.

#### Phase 4: Services rework

- [ ] `ServiceSchedule` read table + header total minutes/week
- [ ] `ServiceEditor` inline structured editor: count + period, minutes, setting select, start/end, notes, owner; writes normalized text into the template's `frequency` / `duration` / `location` columns and parses existing values when it can (falls back to free text when unparseable)
- [ ] Tests: parse/format round-trip, unparseable fallback, total minutes, editor states

**Checkpoint:** edit a service's frequency, minutes and setting; the read table and PDF show the normalized text; completeness still treats frequency/duration as filled.

## Alternative Approaches Considered

- **Explicit Save with a recovery-draft autosave** (offered at the gate): rejected by the user in favor of keeping autosave as the single save path.
- **Bigger fields in place for goals:** rejected; no room for objectives or focused work.
- **New template column types for owner/objectives:** rejected; it would need template-admin changes and per-district template edits. Reserved row keys work for every template that tags the semantics.
- **Structured service storage keys:** rejected in favor of normalized text in the existing columns, which keeps the PDF, AI and completeness untouched.
- **Owner from all school staff:** rejected; owners come from the student's team (access already aligned).

## System-Wide Impact

### Interaction Graph

Section Edit opens field editors, whose per-field `useAutosave` calls `PUT …/values` (`rowVersion`). The server's `DocumentInstanceService` normalizes rows, validates owners against `StudentTeamMembers`, assigns `_rowId`s for rows and objectives, and rotates `rowVersion`. The client merges values back, completeness recomputes, and the strip updates. On finalize: `AuthoredDocumentVersionService` snapshot, then `GoalRecordService` projection (owner), then PDF. The shared draft render is used by family review.

### Error & Failure Propagation

- A save error shows in the section header, plus the existing page-level error state.
- A 409 latches the existing "changed elsewhere" notice; open sections stay open but read-only until reload.
- A failed Discard restore shows an error and keeps the section open, with the snapshot retained.
- An invalid owner is dropped server-side, and the warning surfaces next to the picker.

### State Lifecycle Risks

- Discard after another user's concurrent save hits a 409 (protected by `rowVersion`), so it never silently overwrites someone else's edit.
- Objective ids follow the documented adoption rules to avoid the 2026-09-15 focus-loss / duplicate-id class.
- Owner removal from the team leaves a stale `_ownerUserId` until the next save; the read view shows "Former team member" and completeness flags it.

### API Surface Parity

- No new endpoints. The `PUT /api/documents/{id}/values` contract gains the reserved keys (documented in the DTO comments).
- `GET /api/educator/students/{id}/team` is reused.
- The Bruno collection documents the reserved keys on the values request.

### Integration Test Scenarios

1. Two editors: A opens Goals, B saves Services, A's next autosave gets a 409 → conflict notice; A reloads and their edit is not lost if it was flushed before.
2. Discard after autosave restores the exact pre-Edit values, including a deleted objective's id.
3. Prefill into an amendment carries owners and objectives; an owner removed from the team is dropped.
4. Finalize with open sections: everything is flushed, sections close, the PDF shows objectives and role-only owners, and `GoalRecord.OwnerUserId` is set.

## Acceptance Criteria

### Functional Requirements

- [ ] Completeness is a strip under the header with counts and jump links; no right rail; chat opens in a drawer.
- [ ] Every section renders read-only with Edit; Edit/Done/Discard work independently per section, and several can be open at once.
- [ ] Edits autosave while open; Done flushes and closes; Discard restores the pre-Edit snapshot (with confirmation after autosaves).
- [ ] Goals: card list + focused editor with rich-text statement/baseline/target, measurement, reporting, timeframe, owner, and ordered objectives (add/remove/reorder) that survive reload and finalize.
- [ ] Services: schedule table + structured editor writing normalized frequency/duration/location text; header shows total minutes/week.
- [ ] Goals, services, accommodations and transition rows have an owner chosen from the student's team; non-members are rejected; owners carry forward.
- [ ] Family draft and PDF show objectives and role-only owners.
- [ ] Completeness gains the "no owner" and "no objectives" advisories, with the client and server in agreement.

### Non-Functional Requirements

- [ ] At 1920×1080 the content column is ≥1000px; at 1366×768 the layout has no horizontal scroll.
- [ ] Keyboard: Edit/Done/Discard reachable; E toggles edit; focus moves into the editor and back; contrast ≥ slate-500; detector clean.
- [ ] No regression of the row-identity invariants (focus retained while typing in a goal objective during autosave).

### Quality Gates

- [ ] Vitest coverage for every state listed in Design direction; service tests for normalization, prefill, GoalRecord owner, PDF and completeness parity.
- [ ] `/sht:review` with 0 P1/P2; `npm run test:types` and the CI lint baseline unchanged.
- [ ] After merge: apply the `AddGoalRecordOwner` migration to QA and restart (see memory: QA deploy does not migrate).

## Success Metrics

- Educators can read a full IEP without opening an editor; editing Goals and Services no longer requires scrolling inside small text boxes.
- Every goal and service in the Maple Ridge demo IEPs has an owner after re-seeding.

## Dependencies & Prerequisites

- The student's team exists (Plan 3, PR #25). Demo district seed (`seed-demo`) may need owners and objectives added to look right in demos (follow-up; not required for this plan).

## Risk Analysis & Mitigation

| Risk | Mitigation |
|---|---|
| Discard races autosave or another editor | Snapshot restore goes through `rowVersion`; a 409 shows a conflict instead of overwriting |
| Nested objectives repeat the 2026-09-15 row-identity bug | Same three invariants, plus a focus-retention regression test for objectives |
| Rich text in table cells breaks PDF/AI that expect plain text | Markdown strings; verify the PDF table-cell renderer, and strip markdown for AI prompts where a cell is used as plain text |
| Owner names leak to family outputs | Role-only rendering, with a test on the draft and PDF |
| Completeness client/server drift | Parity test over the same fixtures |

## Future Considerations

- A "My assigned items" view across students for owners.
- Progress reporting per goal using `GoalRecord.OwnerUserId` and objectives.
- Seed demo IEPs with owners and objectives.

## Sources & References

- **Design:** [docs/designs/2026-10-02-iep-authoring-read-edit-sections-design.md](../designs/2026-10-02-iep-authoring-read-edit-sections-design.md). Approved 2026-10-02: autosave + Done/Discard, goal cards + focused editor, objectives per goal, owners from the student's team, role-only owners on family/PDF outputs, `GoalRecord` owner.
- **Mockup:** [docs/designs/2026-10-02-iep-authoring-read-edit-sections-mockup.html](../designs/2026-10-02-iep-authoring-read-edit-sections-mockup.html) (v1, approved with no annotations).
- **Code:**
  - `web/src/features/document-authoring/components/document-editor.tsx:55-285`, `components/completeness-panel.tsx`, `components/section-navigator.tsx`, `components/field-renderers/table-field.tsx:165-595`, `field-renderers/rich-text-field.tsx`;
  - `hooks/use-autosave.ts`, `lib/completeness.ts`, `lib/table-rows.ts`, `lib/section-dom.ts`;
  - `web/src/features/admin/templates/document-semantics.ts`;
  - `api/IepAssistant.Services/Models/DocumentSemantics.cs:102-106`, `Implementations/DocumentInstanceService.cs:400-470`, `DocumentPrefillService.cs`, `GoalRecordService.cs`, `AuthoredDocumentPdfDocument.cs`, `DocumentCompletenessService`;
  - `api/IepAssistant.Domain/Entities/GoalRecord.cs`, `StudentTeamMember.cs`; `api/IepAssistant.Api/Controllers/EducatorController.cs:249-336`.
- **Learnings:** `docs/solutions/ui-bugs/2026-09-15-autosaved-list-rows-server-assigned-ids-react-key-remount-and-positional-adoption.md` (row-identity invariants).
- **Related plan:** `docs/plans/2026-09-15-001-feat-authoring-spine-template-editor-ai-parent-access-plan.md` (semantic tags, `_rowId`, card-mode blocks).
