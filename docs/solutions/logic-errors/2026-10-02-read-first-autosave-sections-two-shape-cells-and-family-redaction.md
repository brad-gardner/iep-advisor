---
module: "Document authoring (IEP editor)"
date: "2026-10-02"
problem_type: logic_error
component: frontend_react
symptoms:
  - "Any edit to a reloaded goal that already had objectives failed to save, and autosave retried forever"
  - "Closing and reopening a goal showed its objectives blank; the next edit deleted them on the server"
  - "Typing the letter 'e' did nothing in any rich-text (TipTap) field"
  - "'Discard changes' right after typing kept the edit; Done/Finalize closed sections after a failed save"
  - "Parents' finalized-IEP view received raw staff owner ids; family re-share summaries said nothing changed after objective edits"
root_cause: logic_error
resolution_type: code_fix
severity: critical
tags: [document-authoring, autosave, row-identity, contenteditable, tiptap, keyboard-shortcut, discard, redaction, change-summary, react, typescript]
---

# Troubleshooting: read-first autosaving sections, two-shape JSON cells, and family-facing redaction

## Problem

PR "IEP authoring — read-first sections, focused goal/service editors, item owners" (plan `docs/plans/2026-10-02-002-*`) reworked `/educator/documents/:instanceId`:

- sections render read-only with Edit → Done / Discard, and autosave keeps running while a section is open;
- goals gained an `_objectives` list;
- goal, service, accommodation and transition rows gained a team-member `_ownerUserId`.

Three independent review passes found that the first implementation had passing tests but lost data and blocked typing. Most of these defects came from patterns that tests built on mocks cannot see.

## Environment

- Web: React 19 + TypeScript 5.9, Vitest/jsdom; the TipTap editor is replaced by a `<textarea>` in `web/src/test/setup.ts`.
- API: .NET 9 / EF Core 9; document values live in `DocumentInstance.ValuesJson`, with reserved row keys next to `_rowId`.
- Date: 2026-10-02

## Symptoms

- Review reproduced these by bundling `web/src/features/document-authoring/lib/objective-rows.ts` with esbuild and running it in node:
  - `toPlainObjectives(plainServerArray)` threw "Cannot read properties of undefined (reading 'description')";
  - `coerceObjectives(keyedArray)` returned blank objectives with no `_rowId`.
- The window-level "E" (open section) shortcut called `preventDefault()` on keystrokes typed inside the TipTap contenteditable.
- Discard checked a `dirty` flag that only turned true after a save *resolved*. A save already in flight, or a debounced save flushed on unmount, landed after Discard had closed the section.
- `GET /api/authored-versions/{id}` returned `ValuesJson` verbatim to linked parents. Only shared-draft revisions went through `FamilyFacingValueRedactor`.
- `ChangeSummaryBuilder.RowContentEqual` skipped every key in `RowMetaKeys.All`, which now included `_objectives` and `_ownerUserId`.

## What Didn't Work

- **Widening the cell type to `unknown[]` and casting with `as KeyedObjective[]`.** The type-check passed, which hid the fact that a cell holds the plain server shape `{_rowId, description, criteria, targetDate}` until it is edited, and the keyed edit shape `{key, cells}` afterwards.
- **Tests that edited objectives before asserting.** That step converted the cell to the keyed shape, so the plain-shape path was never exercised.
- **A single "last save ok" flag (pass-1 fix) for Done/Finalize.** Field A fails, field B then saves fine, and the flag reads ok, so the section closes and A's edit is lost. Per-field tracking replaced it in pass 2.
- **One comma-joined focus selector including `button` (pass-1 fix for focus on Edit).** `querySelector` returns the first match in document order, which was the TipTap toolbar's Bold button.

## Solution

1. **Two-shape cells.** Export one runtime guard, `isKeyedObjective`, from `lib/objective-rows.ts`, and use it everywhere a cell is read or written:
   - `coerceObjectives` passes keyed items through unchanged (their keys survive a remount);
   - `toSaveableCells` sends plain arrays untouched;
   - `adoptRowObjectiveIds` adopts only when both the sent and the current arrays are keyed.
2. **Shortcuts.** Skip the global handler when `e.target.isContentEditable || e.target.closest('[contenteditable="true"]')`, in addition to INPUT/TEXTAREA/SELECT. jsdom has no `isContentEditable`, so the `closest` check is the one the tests can exercise.
3. **Section editing (`section-card.tsx`):**
   - Discard first awaits `sectionFlushRegistry.flushAll()`, then decides from `saveAttemptedRef` (set when `wrappedSave` starts, not when it resolves). The restore happens after the flush, so the unmount flush is a no-op.
   - Failures are tracked per field (`failedFieldsRef`). Done, and Finalize via `useSectionEditing.hasFailures()`, stay blocked until each failed field is retried.
   - Focus on Edit looks for editable controls first and falls back to buttons only when there are none.
4. **Positional id adoption.** Before pairing sent and saved objectives by index, filter `sent` with the server's keep-rule (`keptByServer`: drop entries that are blank after trim, cap at 20).
5. **Family-facing data.**
   - `AuthoredDocumentVersionService.GetVersionAsync` redacts owners to roles on the parent and student path.
   - At finalize, `OwnerEligibleRowSanitizer.StripInactiveOwners` cleans owners in the *frozen snapshot* using the same active-team set as the GoalRecord projection, so the version, PDF and GoalRecord agree.
6. **Change summaries.** `RowContentEqual` uses its own `MetadataOnlyKeys` (`_rowId`, `_carriedFrom`, `_confirmed`, `_ownerRole`), compares `_objectives` by content (ignoring objective ids), and counts owner changes.

## Verification

- API: `dotnet test IepAssistant.Services.Tests`, 1345 passed.
- Web: `npx vitest run`, 953 passed. `npm run test:types` and `tsc -b` are clean, ESLint stays at the CI baseline of 36, and the impeccable detector is clean.
- New regression tests cover:
  - a reloaded goal with plain objectives where only the goal text is edited;
  - Done then Edit again keeps the objectives and their `_rowId`;
  - keydown on a contenteditable is not default-prevented;
  - Discard with a save pending or in flight;
  - field A fails then field B succeeds;
  - Finalize blocked by a section failure;
  - a blank objective before a filled one;
  - parent vs staff version reads;
  - objective-only and owner-only change summaries.
- Migration `AddGoalRecordOwner` was applied to QA SQL Server before merge, so it no longer depends on the post-merge step that bit PR #36.
- **Open at the review cap (todos/243), not fixed in this PR:**
  - a successful Discard leaves the section's failed-field record, so Finalize stays blocked and the closed card offers a Retry that would undo the Discard (fix: clear the map on restore success and gate the banner on `isOpen`);
  - Retry can resend an older failed value after a newer edit to the same field (fix: `flushAll()` before building the Retry patch).
- **Not verified:** real-browser and screen-reader behaviour. The width numbers come from Tailwind arithmetic, not a measured render.

## Why This Works

1. **Root causes:**
   - the type system was told a cell had one shape when it really had two;
   - the tests' mocks (a textarea instead of a contenteditable, and fixtures already in the edit shape) matched the implementation's assumptions instead of production;
   - save state was modeled as "the last thing that happened" rather than "what is still unsaved";
   - redaction and owner validation were applied to some read and freeze paths but not all of them.
2. **Why the fix holds:**
   - a single runtime guard makes every consumer handle both shapes;
   - deciding from "attempted" and per-field failure state reflects what has actually been sent and what is still unsaved;
   - redaction and sanitization now run at the boundary (every family read, every finalize) instead of per feature.

## Prevention

- When a stored JSON value can be in more than one runtime shape, write a guard and use it at every read and write site. Never widen to `unknown[]` and cast.
- Write at least one test fixture in the exact shape the server returns, and run it through the unedited path.
- Global keyboard shortcuts must ignore any contenteditable ancestor. If the app's real editor is mocked out in tests, add one test against a real contenteditable.
- For autosave UIs: a destructive action (Discard) flushes first and decides from "attempted". Gates (Done, Finalize) read per-field unresolved failures, never a last-outcome flag.
- Any new family- or student-facing read of `ValuesJson` must go through `FamilyFacingValueRedactor`. Any finalize step that projects data must also sanitize the frozen snapshot.
- When reserved row keys carry content (not metadata), give diffing and summary code its own exclusion list.

## Related

- `docs/solutions/ui-bugs/2026-09-15-autosaved-list-rows-server-assigned-ids-react-key-remount-and-positional-adoption.md`: row-identity invariants, extended here to nested objectives.
- `docs/solutions/logic-errors/2026-10-02-one-analysis-engine-race-safe-runs-and-a-backfill-that-converges.md`: the same day's lesson on conditional state transitions.
- Plan: `docs/plans/2026-10-02-002-feat-iep-authoring-read-first-sections-goals-services-owners-plan.md`. Design and mockup: `docs/designs/2026-10-02-iep-authoring-read-edit-sections-*`.
