# Design Discussion: Page-Level IA, Layout & Interaction Rework

**Date:** 2026-07-02
**Feature:** Add layout/interaction primitives (Modal, Drawer, ConfirmDialog, Table, DetailLayout) + widen the shell, then recompose every list/detail/form/dashboard so the app stops feeling sparse and ad-hoc.
**Origin:** `docs/brainstorms/2026-07-02-page-layout-ia-interaction-brainstorm.md` (conventions decided there)

## Current State

- **No overlay primitives exist.** `components/ui/` has no Modal/Drawer/Dialog. The only `createPortal` is the toast viewport (`toast-viewport.tsx:61`, portals to `document.body`). There is **no focus-trap, no scroll-lock, no global Escape handler** anywhere; the one hand-rolled outside-click is the iep-viewer compare dropdown. z-index tops out at `z-50` (sidebar drawer/backdrop `z-40/50`, toast `z-50`).
- **Forms render inline.** ~11 create/edit forms sit on the page — some as always-on `<Card className="max-w-lg">` blocks (add school/student/staff/parent), some toggled by a `useState` boolean (new IEP, new report, add goal, admin invite). Contracts vary: `school-form`/`advocacy-goal-form` are clean controlled forms (`onSubmit → Promise<{success,error}>`, have `onCancel`/`submitLabel`); `share-child-dialog`/`create-progress-report-form`/`create-iep-form` own their API call + `useToast`; four wrap themselves in `<Card max-w-lg>` + own `<h2>`; `finalize-dialog` is a sync `void` submit with parent-lifted state.
- **Confirmations:** 13 native `confirm()` calls (deletes/revokes across authoring, documents, sharing, students) + several inline two-step "Deactivate?" toggles (`school-row`, `staff-row`, `invite-row`, admin detail).
- **Lists are `<ul>`-of-`<Card>`** (schools, students, staff, users, children); **no sortable table anywhere** (only 2 static presentational `<table>`s). Row field/action inventory captured for column mapping.
- **Shell** = `main-layout.tsx:22` `max-w-5xl mx-auto …`. `max-w-lg` (16 files) and `max-w-md` (20 files) are the sparse-width culprits. Detail pages stack lone `max-w-lg` cards down the left of the 64rem shell.
- **All open/close state is local `useState`;** URL is used only for tokens + the `?attention` roster filter. Vitest+jsdom test layer exists (MemoryRouter + `vi.mock` render pattern).

## Patterns to Follow

- **Native `<dialog>` + `.showModal()`** as the substrate for Modal/Drawer/ConfirmDialog (WAI-ARIA APG + the a11y research's top recommendation): browser gives focus-trap, background `inert`, top-layer stacking, and return-focus for free — we only wire labelling, backdrop-click, reduced-motion, scroll-lock, and `alertdialog` semantics. Minimal-deps ✔ (`[[feedback-minimal-dependencies]]`).
- **Native `<table>`** with `<th scope>` + a `<button>`-in-`<th>` sortable header (`aria-sort`), not an ARIA grid (APG sortable-table).
- Reuse the **toast portal** approach, the **mobile-sidebar drawer** as the slide/backdrop animation reference, the existing **`onSubmit → Promise<{success,error}>`** form contract, `PageHeader`'s **`actions` slot** for the `+ Add` trigger, and the **`cn()`/`Record<Variant>`** styling convention.
- Test pattern: colocated `*.test.tsx`, `render` + `MemoryRouter`, `vi.mock` hooks, plus `ToastProvider` wrapper where a form uses `useToast`.

## Desired End State

- **`Modal`** — native `<dialog>`; `title` (→ `aria-labelledby`), body, footer actions; Esc + backdrop close; `open` controlled prop; scroll-lock; reduced-motion. Home for **short** forms + as the base for ConfirmDialog.
- **`Drawer`** — same `<dialog>` semantics, edge-anchored, slides from right; home for **long/multi-section** forms; keeps list context.
- **`ConfirmDialog`** — `role="alertdialog"`, `aria-describedby` on the consequence text, **initial focus on Cancel**, action-named destructive button ("Delete report"); replaces the 13 `confirm()` sites.
- **`Table`** — native `<table>`, sortable column headers (client-side sort at pilot scale, `aria-sort`, shape-not-color indicator), row hover, a **row-actions kebab** (menu-button pattern) or a visible actions column *(open question)*; responsive = horizontal-scroll with frozen first column under `md`. Default for schools/students/staff/users/children/documents lists.
- **`DetailLayout`** — two-column (main + right sidebar for status/metadata/quick-actions); **source order main→sidebar**; collapses to stacked under `md`. For student/child/user detail.
- **Shell widened** `max-w-5xl → max-w-7xl` in `main-layout.tsx`; **content-width is per-region** — data-dense pages fill the width; long-form/reading blocks (IEP/ETR/PDF viewers) cap at ~`65ch` for readability (WCAG 1.4.4-safe, `ch` not px).
- **Forms re-parented**: drop each form's self-`<Card max-w-lg>`+heading, standardize on the controlled `onSubmit → Promise<{success,error}>` contract, and mount inside a Modal/Drawer opened from the page header. Inline form cards + the one-off `/children/new` page retired; **wizards (onboarding, district-setup) stay full-screen**.

## Design Decisions

- **Native `<dialog>` substrate** — eliminates the four highest-risk hand-rolled a11y bugs (focus trap, inert, stacking, return-focus). Feature-guard `showModal()` for jsdom.
- **Modal vs Drawer split (concrete):** Modal for ≤~5 fields / single-purpose (all invites, add school, add student, add goal, confirms); Drawer for multi-section/long (create-child [6 fields], create-IEP [meta + attendees], create-progress-report). One rule, keyed to complexity.
- **Open/close state stays local `useState`** (consistent with today) unless we opt into URL-driven modals *(open question)*.
- **`ConfirmDialog` for the 13 `confirm()` sites**; the existing inline two-step "Deactivate?" toggles in rows may stay (already custom, lightweight) or converge on ConfirmDialog *(decide in plan; lean convert for consistency)*.
- **Table sort is client-side** (pilot data volumes are small; server-side paging is a later concern).
- **Row navigation + row actions coexist:** row click navigates (where it does today); the kebab/actions cell `stopPropagation`.
- **No new dependency** — no Radix/Headless UI/focus-trap/react-modal; all in-repo on `<dialog>`.
- **No rebrand** — same palette/type; layout + interaction only. New primitives: Modal, Drawer, ConfirmDialog, Table, DetailLayout (+ maybe a small Menu for the kebab).

## Resolved Questions

1. **Row actions → kebab (⋮)**, backed by a **new reusable `Menu` (menu-button) primitive** (APG: `aria-haspopup`, arrow-key nav, Esc, return-focus). It also replaces the app's several hand-rolled dropdowns.
2. **Modal/drawer open-state → local `useState`** (matches the whole codebase; no URL-driven UI state today).
3. **Overlay tests → jsdom + browser-delegated.** Unit tests cover render/aria/controlled-open/Esc/backdrop handlers + initial-focus target; the real focus-trap/`inert` is the browser's job, validated in the ship-time e2e/manual pass. No Vitest browser-mode this pass.
4. **Scope → whole app** (all lists→tables, all details→two-column, all forms→modal/drawer, all `confirm()`→ConfirmDialog, dashboards densified), one branch.

**New primitives total:** `Modal`, `Drawer`, `ConfirmDialog`, `Table`, `Menu`, `DetailLayout` (+ shell widen).

## Testing Strategy

- **Primitives (Vitest + RTL):** render, controlled open/close, `role`/`aria-modal`/`aria-labelledby`/`aria-describedby` wiring, Esc + backdrop `onClose`, initial-focus target present, ConfirmDialog focuses Cancel, Table `aria-sort` toggles + sort order, kebab menu open/Esc/return-focus. Guard `showModal()` for jsdom; note the trap is browser-delegated.
- **Migrated screens:** the `e2e/` Playwright suite stays green (every `data-testid` preserved when forms move into overlays; the `+ Add` trigger + modal fields keep their testids). Add e2e for "open modal → submit → toast → row appears" and a delete→ConfirmDialog flow on one screen.
- **Consistency guard** extended: no inline `<Card max-w-lg>` create-forms and no `window.confirm(` in migrated dirs.
- **Visual/manual + axe** pass per screen type (stack-dependent, ship-time).
