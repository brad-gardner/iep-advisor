# Design Discussion: IEP authoring page — read-first sections, focused editors, item owners

**Date:** 2026-10-02
**Feature:** Rework the district IEP authoring page. Completeness moves to the top and the editing column gets the width. Sections render read-only, each with its own Edit toggle. Edits autosave, and Done closes the section. Goals and Services get focused structured editors (goals gain objectives/benchmarks). Goals, Services, Accommodations and Transition items each get a team-member owner.
**Source:** user request + 1080p screenshot of `/educator/documents/:instanceId` (Edit tab), 2026-10-02.

## Current State

- **Route:** `/educator/documents/:instanceId` → `DocumentEditorPage` (`web/src/features/document-authoring/pages/document-editor-page.tsx`), with Edit/Converge tabs.
- **Page body:** `DocumentEditor` (`components/document-editor.tsx:55-285`). Header (title, status badge, `AutosaveIndicator`, Share with family / Evidence / Ask the assistant), `ProposedEditsPanel`, then a bespoke grid `lg:grid-cols-[14rem_minmax(0,1fr)_16rem]` (+22rem chat column when open):
  - `SectionNavigator` (sticky, scroll-spy, `[`/`]` keys);
  - section `Card`s with always-on field editors;
  - `CompletenessPanel` (16rem rail).
  - At 1080p the content column is about 690px wide, with large empty margins.
- **Fields:** `field-renderers/*` dispatch by template field type (Text, RichText via lazy TipTap, Date, Select, Checkbox, Table). **Every field is always editable and autosaves on its own** (`use-autosave.ts`, 700ms debounce; flush on blur, navigation and finalize) to `PUT /api/documents/{id}/values`, with `rowVersion` optimistic concurrency. A 409 latches a "changed elsewhere" reload notice.
- **Goals/Services/Accommodations/Transition** are template **Table** fields with block semantics (`document-semantics.ts`). They render as stacked cards (`table-field.tsx:184-345`) whose prose cells are plain 2-row `<textarea>`s. That is why the editors feel too small. Table columns can only be Text, Date, Select or Checkbox.
- **Row identity:** server `_rowId` inside the row cells, plus `_carriedFrom`/`_confirmed` for prefill provenance. The rules in `docs/solutions/ui-bugs/2026-09-15-autosaved-list-rows-*.md` are load-bearing:
  1. the React key never changes;
  2. the save payload is read from a synchronous ref;
  3. ids are adopted by correlating to the exact array that was sent.
- **Completeness:** client `lib/completeness.ts` computes from **saved** values. Server `DocumentCompletenessService` mirrors it for the home dashboard; the two must stay in sync.
- **Owners:** no owner field exists. The student's team is available via `GET /api/educator/students/{id}/team` (`StudentTeamMember` with `TeamRole`). Precedent for an owner field: `OwnerUserId` + `OwnerName` on obligations.
- **Goal lineage:** `GoalRecord` is projected from goal rows at finalize, keyed by `_rowId`.

## Patterns to Follow

- **Saving:** keep `useAutosave` + `useFlushRegistry` + `PUT …/values` + `rowVersion` as the only save path. No second save endpoint.
- **Row identity:** keep the three row-identity rules above in every new row editor (goal objectives are nested rows, so they need their own stable keys).
- **Layout:** `DetailLayout` conventions (main-first DOM order), `Card`, `Badge`, `Button`, `Notice` and `Drawer` (`web/src/components/ui`). Tokens in `web/tailwind.config.js`: `brand-teal`/`slate`/`amber`/`danger`, Lora serif headings, DM Sans body, `rounded-card`. Contrast floor `slate-500`+ (PR #33).
- **Prose fields:** the TipTap `RichTextEditor` that narrative fields already use (lazy-loaded).
- **Semantics:** add new reserved row keys next to `_rowId`, and new column semantics in `document-semantics.ts` mirrored to `DocumentSemantics.cs`, rather than hard-coding field GUIDs.

## Desired End State

1. **Page frame:**
   - Header: title, status, save state and actions.
   - A **completeness strip** below the header: the percent, a progress bar, counts of required and advisory items, and a "Show items" disclosure that lists the flagged items and jumps to them.
   - Proposed edits from meetings.
   - A two-column layout: a **section navigator** (14rem, sticky) and a **wide content column** (up to about 1100px at 1080p, more on wider screens).
   - The assistant chat becomes a right-side `Drawer` instead of a fourth grid column.
2. **Sections are read-first:**
   - Each section card shows its content as formatted read-only text (lists, headings) and an **Edit** button. Empty sections show a quiet "Not started — Edit" prompt.
   - While editing, the card switches to edit mode (field editors, AI help, Pull from student). Its header shows the save state ("Saving… / Saved 2s ago") and **Done** and **Discard changes** buttons.
   - Several sections can be open at once, and each is independent.
   - Leaving the page or switching tabs flushes the pending autosaves, as today.
3. **Saving:** edits autosave while a section is open (existing debounce plus an idle flush every few seconds). **Done** flushes and returns the section to read view. **Discard changes** restores the section's values to the snapshot taken when Edit was clicked and saves that, with a confirmation step if anything has already autosaved. A 409 conflict keeps today's reload notice.
4. **Goals:**
   - The read view is a list of goal cards: number, area, goal statement (clamped to a few lines), measurement and timeframe chips, owner avatar and name, objectives count, and carried-forward/keep badges.
   - **Edit** on a goal opens the **goal editor** across the full content width. It has:
     - a large auto-growing goal statement (rich text);
     - baseline and target criteria side by side (rich text);
     - measurement method and progress-report schedule;
     - timeframe;
     - an **owner** picker;
     - **objectives/benchmarks**: an ordered list of {description, criteria, target date}; add, remove and reorder.
   - Per-field AI help and evidence stay available. Add goal / remove goal (with a reason for persisted goals) keep today's rules.
5. **Services:**
   - The read view is a **service schedule**: one row per service with type, provider role, frequency (number × per week/month), minutes per session, location (general/special education setting), dates and owner.
   - **Edit** opens a focused service editor with structured inputs (frequency count + period, minutes, location select, start/end dates), a notes field and an **owner** picker.
6. **Accommodations:** read view grouped by category. The editor gains an **owner**, and the accommodation text becomes a larger auto-growing field.
7. **Transition:** read view per goal area. The editor gains an **owner** (the person responsible for the transition service), with larger prose fields.
8. **Owner model:**
   - Reserved row cell `_ownerUserId` (number), next to `_rowId`, on rows of the four block semantics. The display name is resolved from the student's team, not stored.
   - Prefill carries the owner forward with the row. Invalid owners (not on the team) are rejected server-side on save.
   - The editor shows the owner's name. The shared family draft and the finalized PDF show the owner's **role** only ("Responsible: Intervention specialist").
9. **Objectives model:** a reserved row cell `_objectives` (an array of `{ _rowId, description, criteria, targetDate }`) inside the goal row, with server-assigned objective ids following the same adoption rules.
10. **Completeness:**
    - The client and server rules gain advisory checks: "goal has no owner", "service has no owner", "goal has no objectives" (advisory only).
    - The percent still counts fields.
    - The strip reflects saved values, and is marked "updating…" while a section has unsaved edits.

## Design Decisions

- **Sections are read-first with per-section edit**, decided with the user, and autosave is kept as the save path. One persistence path means no draft table and no second conflict model, and nothing is lost if someone forgets Done.
- **Goals get cards plus a focused editor, with objectives**, decided with the user. Goals are the most complex part of the IEP and need room.
- **Owners are the student's IEP team members**, decided with the user, stored per row and carried forward by prefill. A "My assigned items" view is out of scope.
- **Owners and objectives are stored as reserved row keys, not new template column types.** That needs no template-admin change and works for every district template that tags these semantics. Templates that don't tag the semantics simply don't get owners or objectives.
- **Rich text for goal statement, baseline and target.** These cells become `RichText`-capable for block semantics, stored as markdown strings in the same cell. The PDF already renders markdown for narrative fields.
- **Chat moves to a drawer**, so the content column keeps its width.
- **Completeness moves to a strip under the header**, collapsible, with jump links.
- **No schema migration needed:** values are JSON in `DocumentInstance.ValuesJson`. Server validation and normalization change: reserved-key allow-list, owner-on-team check, objective id assignment.

## Resolved Questions (2026-10-02, accepted with design approval)

1. **Family draft and finalized PDF:** both show objectives. Owners appear by role only ("Responsible: Intervention specialist"), never by personal name.
2. **Discard changes:** always offered. It confirms when edits have already autosaved, then restores the snapshot taken when Edit was clicked and saves it.
3. **GoalRecord:** gains a nullable `OwnerUserId`, set at finalize (one additive migration). Objectives stay in the finalized snapshot JSON.

**Mockup:** `docs/designs/2026-10-02-iep-authoring-read-edit-sections-mockup.html` (v1). Approved by the user ("looks good") with no annotations.

**Design approved:** 2026-10-02.

## Testing Strategy

- **Vitest:**
  - section read/edit toggle (read render per field type, Edit/Done/Discard, several sections open);
  - autosave while editing, and flush on Done;
  - completeness strip and jump links;
  - goal editor (objectives add/remove/reorder with stable keys, owner picker from team);
  - service editor (structured frequency/minutes);
  - owner picker for accommodations and transition;
  - keyboard and focus (Edit moves focus into the editor; Done returns focus to the Edit button).
- **Service tests:** values normalization accepts `_ownerUserId` only for team members, assigns objective `_rowId`s, and prefill carries owners and objectives forward; completeness parity between client and server for the new advisory checks.
- **Manual on QA:** the Maple Ridge demo district at 1920×1080 and 1366×768.
