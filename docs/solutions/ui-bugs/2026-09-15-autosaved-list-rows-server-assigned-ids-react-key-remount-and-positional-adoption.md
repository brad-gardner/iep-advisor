---
module: "DocumentAuthoring"
date: "2026-09-15"
problem_type: "ui_bug"
component: "react_component"
symptoms:
  - "Typing into a freshly added goal row stops after ~300 ms (focus drops to <body>) and further keystrokes are lost"
  - "AI help on a goal row intermittently fails with 'Row not found' right after the row was added"
  - "The persisted _rowId for a row alternates between two GUIDs across successive autosaves"
  - "Removing a row while a save is in flight makes the next row adopt the deleted row's id"
root_cause: "async_timing"
resolution_type: "code_fix"
severity: "high"
tags: [react, autosave, react-key, row-identity, race-condition, debounce, template-editor, code-review]
---

# Troubleshooting: Autosaved list rows that adopt server-assigned ids — React key remount and positional id adoption

## Problem
The template document editor renders repeating blocks (goals, services, accommodations) as client-side rows that are debounced-autosaved as one array. The server assigns each row a stable `_rowId` on first save and echoes it back. The first implementation adopted those ids **by array position** into whatever rows were in local state when the response arrived, and **rewrote the row's React `key`** to the new GUID. Independent review (react-async-reviewer, react-reviewer) found this produced a P1 and a P2 before it ever shipped.

## Environment
- Module: DocumentAuthoring (`web/src/features/document-authoring`)
- React 19.2, Vite 7, custom `useAutosave` debounce hook (`web/src/hooks/use-autosave.ts`, 700 ms, serialized, rerun-on-newer-value)
- Server: `DocumentInstanceService.CoerceTable` (.NET 9) preserves row order, keeps a valid `_rowId`, and mints a fresh GUID for any row without one
- Affected component: `components/field-renderers/table-field.tsx` (+ `lib/table-rows.ts` after the fix)
- Date: 2026-09-15

## Symptoms
- Click "Add goal", click into the new goal text and type: the `<li>` remounts when the add-save resolves, the textarea loses focus and keystrokes go nowhere (the `[`/`]` section shortcut even started firing because the target was no longer an input).
- "AI help" on the row posted a `rowId` the server no longer had (`Row not found`), because the debounced edit queued *before* adoption re-sent the row **without** its id and the server minted a second GUID.
- Removing a row while a save was in flight shifted the response ids onto the wrong local rows.

## What Didn't Work

**Attempted Solution 1:** Adopt ids after each save with `setRows(current => adoptRowIds(current, saved))` and use the server id as the React key so identity "survives reloads".
- **Why it failed:** (a) a React key is *component identity* — changing it from `row-N` to a GUID unmounts and remounts the row, destroying the focused input; (b) `current` (state at response time) is not the array that was sent, so positional pairing is wrong whenever rows were added/removed in between; (c) `useAutosave` snapshots the value at `save()` time, so an edit queued before adoption still carried no `_rowId`.

## Solution

Three invariants, implemented in `web/src/features/document-authoring/lib/table-rows.ts` and `table-field.tsx`:

1. **A row's React key never changes after mount.** `coerceRows` picks the key once (server `_rowId` if present, else a temporary `row-N`); `adoptRowIds` only writes the id into `cells[_rowId]`.
2. **The save payload is read at send time from a synchronously-written ref**, not from the value queued at edit time. Every mutation goes through `mutate(updater)` which writes `rowsRef.current` and then `setRows`; the autosave value is just a trigger.
3. **Ids are adopted by matching the rows that were actually sent**, index-for-index against the response (the server preserves order), then applied to whichever of those rows still exist and lack an id. Rows removed meanwhile are absent from `current`; rows added after the request have no entry and are untouched.

```text
// Before (broken): positional adoption + key rewrite
setRows(current => current.map((row, i) => {
  const id = saved[i]?._rowId;
  return id ? { key: id, cells: { ...row.cells, _rowId: id } } : row;   // key changes -> remount
}));

// After (fixed): key is immutable; pair by the SENT rows; payload from rowsRef
const sent = rowsRef.current;
const result = await onSave({ [fieldKey]: sent.map(r => r.cells) });
mutate(current => adoptRowIds(current, sent, result.values?.[fieldKey]));

export function adoptRowIds(current, sent, saved) {
  const idByKey = new Map(sent.map((row, i) => [row.key, saved[i]?._rowId]).filter(([, id]) => id));
  return current.map(row => rowId(row) || !idByKey.has(row.key)
    ? row
    : { key: row.key, cells: { ...row.cells, _rowId: idByKey.get(row.key) } });   // key preserved
}
```

## Verification
- `web/src/features/document-authoring/lib/table-rows.test.ts` (4 tests): keys unchanged after adoption; removal during in-flight save pairs the surviving row with *its* id (not the deleted row's); later-added rows and rows with existing ids untouched; non-array responses ignored.
- `web/src/features/document-authoring/components/field-renderers/table-field.test.tsx`: the same textarea element stays mounted and focused across the adopting save (`expect(screen.getByRole('textbox', …)).toBe(goalInput)`, `document.activeElement === goalInput`).
- Independent pass-2 review (react-async-reviewer) traced the overlapping-save interleavings and "could not construct an interleaving where ids diverge"; it noted the test named "sends the latest rows" is vacuous as written (tracked as P3 in `todos/011-pending-p3-web-editor.md`) — the *stale-payload* half of the fix is verified by code trace, not yet by a regression test.
- Full web suite 119/119, backend 509/509, type-check/lint/build/guard:ux green at `e2403e8`.

## Why This Works
1. **Root cause:** the code conflated three different identities — React reconciliation key, client row identity, and server lineage id — and let an async response mutate state that had moved on. Rewriting the key remounted the row; positional pairing against *current* state paired the wrong rows; the debounce captured a pre-adoption payload.
2. The fix separates them: the key is client identity and immutable; `_rowId` is server identity carried in data; the payload is always the latest rows (ref), and adoption is keyed to the exact request that produced the response.
3. Underlying issue: any "server assigns ids on first save" list with debounced autosave has this shape. The safe pattern is *send from a ref, adopt by request correlation, never touch keys*.

## Prevention
- In list editors, never derive React keys from data that can change after mount. Assign keys once.
- When a debounced saver echoes normalized data, correlate the response to the **request snapshot**, not to current state.
- Keep a synchronous ref mirror for anything a save must read at send time; an effect-synced ref is stale for immediate flushes.
- Write the interleaving test first (add → type while in flight → resolve → assert the next payload carries the id); reviewers found the first version of that test passed vacuously.

## Related
- Plan: `docs/plans/2026-09-15-001-feat-authoring-spine-template-editor-ai-parent-access-plan.md`
- Fix commit: `e2403e8` on `feat/authoring-spine`; review evidence `todos/reviews/2a2f5b05-…`
- Broader lesson from the same plan: a feature-flag cutover (`IEP_AUTHORING_MODE='template'`) shipped a new editor that lacked the AI assist, pull-from-student and parent-view surfaces of the editor it replaced. Cutovers need a parity checklist of user-visible capabilities, not just data-model parity.
