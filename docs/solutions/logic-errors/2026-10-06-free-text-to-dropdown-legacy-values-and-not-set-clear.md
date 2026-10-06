---
module: "Children (parent child profile form)"
date: "2026-10-06"
problem_type: logic_error
component: frontend_react
symptoms:
  - "Choosing 'Not set' on the edit form saved successfully, but the old grade or disability was still there the next time the form opened"
  - "A school-linked child with disability 'Visual Impairment' showed a duplicate 'Visual Impairment (current value)' option next to 'Visual impairment (including blindness)'"
  - "A background reload while the edit modal was open could rebuild the option list without the legacy value, so the select displayed 'Not set' while the form still held and submitted the legacy value"
root_cause: logic_error
resolution_type: code_fix
severity: medium
tags: [dropdown, select, legacy-values, partial-update, empty-string-clear, normalization, child-profile, react, aspnet-core]
---

# Free-text fields to dropdowns: legacy values, server label parity, and making "Not set" actually clear

## Problem

On the parent child form (`web/src/features/children/components/child-form.tsx`), Grade Level and Disability Category changed from free-text inputs to `<Select>` dropdowns. `ChildProfile` still stores free strings, so existing rows contain values like "8", "SLD", "8th grade", or text the platform wrote itself.

The first implementation normalized close variants and kept unmatched values as an extra "(current value)" option. Review then found three gaps.

## Environment

- Web: React 19 + TypeScript 5.9, Vitest/jsdom.
- API: .NET 9, `ChildProfileService.UpdateAsync`, partial update with `[FromBody]` JSON.
- Date: 2026-10-06.

## Symptoms

- **"Not set" did nothing on edit.** The form sent `value.trim() || undefined`, so JSON dropped the field. `UpdateAsync` only assigns `if (model.GradeLevel != null)`, so the stored value survived. The old text input had the same flaw when emptied. A dropdown that lists "Not set" makes it a visible option that does nothing.
- **Server wording didn't match the client labels.** `ChildLinkService` writes `DisabilityCategory.ToDisplay()` into the parent profile. 14 of the 15 strings match the client labels apart from case. `"Visual Impairment"` differs in wording from `"Visual impairment (including blindness)"`, so it fell through as a legacy value.
- **Snapshot drift.** The selected values were `useState` initializers (read once at mount), but the options were `useMemo` keyed on `initialValues`. The parent rebuilds `initialValues` from `child` on every render, and `reloadChild()` can resolve while the modal is open. After a reload, the options and the state described different records.

## What Didn't Work

- **A comment saying "computed once at mount" on a `useMemo` with dependencies.** It recomputes whenever its dependencies change. Only `useState(() => …)` (or `useMemo(…, [])`) actually snapshots.
- **Reading the live `initialValues` prop at submit to decide clear vs. unchanged** (the first version of the clear fix). The same drift applies: a reload that fills a previously empty field would make an untouched "Not set" send `""` and delete a value the user never saw.

## Solution

1. **Three-state update contract.** In `ChildProfileService.UpdateAsync` (grade level and disability only), `null` means unchanged and empty or whitespace means clear (stored as `null`). On the client, the `dropdownValue(value, initialValue)` helper sends `""` only when the selection is "Not set" and the record had a value. Create forms pass no `initialValues`, so they never send `""`.
2. **Alias every server display string.** `SERVER_DISPLAY_LABELS` is typed `Record<DisabilityCategory, string>`, so a missing category fails to compile. A parameterized test asserts that each entry normalizes to its canonical option.
3. **Snapshot everything the form compares against.** The options, the selected values, and the clear baseline (`initialGradeLevel` / `initialDisabilityCategory`) are all mount-time `useState` values. `Modal` unmounts the form while closed, so each edit open starts fresh.

## Verification

- Web: `npx vitest run` passes (1030 tests at the second fix commit). The `children` feature has 81 tests after the final fix, including:
  - "Not set" on edit sends `""`;
  - create "Not set" sends `undefined`;
  - "Visual Impairment" preselects the canonical option;
  - a rerender from `""` to `"5th"` still submits `undefined`.
- `npm run test:types` and `tsc -b` are clean. ESLint stays at the CI baseline of 36.
- API: the new `ChildProfileServiceTests` (real SQLite in memory) cover clear, unchanged, and set.
- Review: 3 passes.
  - Pass 1: 2 P2s, both fixed.
  - Pass 2: 0 P1/P2.
  - Pass 3: covered the final snapshot change.
- **Not verified:**
  - No API-level test PUTs `""` through HTTP. Review confirmed by reading the code that `System.Text.Json` `[FromBody]` binding keeps `""`, because `ConvertEmptyStringToNull` doesn't apply to body input.
  - No real-browser run.

## Why This Works

- **"Not set":** omitting a field and clearing it are different intents. JSON `undefined` can't carry "clear", so the contract needs an explicit empty value. The server has to accept it as clear rather than store `""`.
- **Label parity:** the client's alias table is generated from the same category list the type system checks. Server parity is pinned per category instead of relying on the case-insensitive match working for most entries.
- **Snapshot drift:** when every value the form compares comes from the same version of the record, display, submit, and the clear decision cannot disagree.

## Prevention

- When a free-text field becomes a dropdown over existing data:
  - list every producer of stored values, including server code that writes display strings, and alias each one;
  - keep any value that still doesn't match as its own selected option.
- A "Not set" or "None" option on an edit form needs an explicit clear value end to end. Check what the PATCH/PUT handler does with `null`, omitted, and `""`.
- In a form initialized from props, anything compared against the initial record (options, baselines, dirty checks) must be snapshotted the same way as the state. Don't mix `useState` initializers with `useMemo`/prop reads.
- Still open (P3, pre-existing): on edit, `lastName`, `schoolDistrict`, and `dateOfBirth` still can't be cleared. They use the old `|| undefined` pattern.

## Related

- `docs/solutions/logic-errors/2026-10-02-read-first-autosave-sections-two-shape-cells-and-family-redaction.md`: the same "tests built from the shape the implementation assumes" lesson; here, fixtures in the server's display wording caught the parity gap.
- `api/IepAssistant.Domain/Entities/DisabilityCategory.cs` (`ToDisplay`, `TryParseDisplay`) and `web/src/features/children/lib/child-profile-options.ts`.
