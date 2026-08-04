# Page-Level IA, Layout & Interaction Rework — Brainstorm

**Date:** 2026-07-02
**Status:** Draft
**Precedes:** `/sht:plan`
**Builds on:** the design-system foundation + app-wide primitive migration (2026-07-01/02). Those made the *building blocks* consistent; this reworks *page composition, density, and interaction patterns* — the layer that was deliberately left alone.

## Context

The user's critique after the consistency pass: "it's information architecture, bad UX conventions, poor use of whitespace… add forms just embedded in the page, whitespace everywhere, seemingly random layouts." A composition audit confirmed it, concretely:

- **No modal/dialog exists anywhere.** Every create/edit interaction is inline — either an always-on `Card` at the top of a list page (add school/student/staff-invite/parent-invite) or a toggle-to-reveal card (new IEP, new report, add goal, admin invite). Confirmations use native `confirm()` or inline toggles.
- **Sparse, half-empty pages.** `max-w-lg` (32rem) cards dropped into the `max-w-5xl` (64rem) shell, left-aligned — ~half the width empty. Worst case: `educator-student-detail-page` = six stacked `max-w-lg` sections down the left, right ~40% blank.
- **Lists are stacks of full-width one-line cards** (`ul` of `Card`s), not tables — very low horizontal density. Only `admin-users` is table-ish.
- **`PageLayout` is a single `space-y-6` stack** with no width/columns/tabs/sidebar slots; every page body is composed ad hoc (spacing, width, list rendering each chosen independently). `iep-viewer` bypasses `PageLayout` entirely and hand-rolls header + tabs.

## What We're Building

A page-composition system — new layout/interaction primitives plus a per-screen-type recompose across the **whole authenticated app**, to a calm/trustworthy **productivity-tool** feel (denser and more structured, not a rebrand).

**New primitives:**
- **`Modal`/`Dialog`** — home for **short** create/edit forms + confirmations (centered overlay, focus-trap, Esc/backdrop close, `role="dialog"` + `aria-modal`, portal). Retire inline form cards.
- **`Drawer`** (slide-over) — home for **long/multi-section** forms (create-child, IEP metadata); slides from the right, keeps list context, scrolls internally.
- **`ConfirmDialog`** — replace native `confirm()` and inline "Deactivate?" toggles for destructive actions (a thin `Modal` variant).
- **`Table`** — data table with column headers, client-side sort, row hover, and a row-actions (kebab) affordance; the default for rosters/lists.
- **`DetailLayout`** (or a `PageLayout` two-column variant) — main content + right-hand sidebar (metadata/status/related actions), collapsing to stacked under `md`.
- **Widen the app shell:** `MainLayout` content container `max-w-5xl` → **`max-w-7xl` (80rem)** so tables + two-column details fill the space (document/reading viewers keep a comfortable narrower measure).

**Per-screen-type templates (applied consistently):**
- **List/index** → `Table` filling the shell; primary action (`+ Add …`) in the `PageHeader` actions slot opening a **Modal** form.
- **Detail** → `DetailLayout` two-column (main + sidebar); tabs where a detail has distinct sections (`iep-viewer` moves onto the shared tab pattern).
- **Form/create** → **Modal** for short forms, **Drawer** for long/multi-section (retire always-on inline cards and the one-off `/children/new` page); the two first-run **wizards** (onboarding, district-setup) stay full-screen flows.
- **Dashboard/home** → intentional module grid (already partly there); tighten density and kill lone `max-w-lg` cards.
- **Viewer** (IEP/ETR/PDF/comparison) → keep a comfortable reading width (readability exception to "denser").

## Why This Approach

- The audit shows the pain is **composition**, not tokens — so the fix is layout/interaction primitives + templates, exactly what a design system's second layer provides (Polaris/Linear/Stripe all standardize page frames + tables + modals).
- Modals for forms directly kill "forms embedded in the page" and reclaim the wasted width; tables + two-column details directly kill "whitespace everywhere"; per-type templates kill "random layouts."
- The primitive migration already done makes this cheaper — forms already use `Input`/`Button`; moving them into a `Modal` is re-parenting, not rewriting.

## Key Decisions

- **Short forms → `Modal`; long/multi-section forms → `Drawer`**, opened from a `PageHeader` action. *(User-chosen.)*
- **Destructive actions → `ConfirmDialog`** (retire native `confirm()` / inline toggles).
- **Lists → `Table`** with sortable headers + row actions. *(User-chosen.)*
- **Detail pages → two-column** main + sidebar (stacks under `md`). *(User-chosen.)*
- **Density → denser, structured** — tables, side-by-side sections, no lone narrow cards in the wide shell. *(User-chosen.)*
- **App shell widened `max-w-5xl` → `max-w-7xl`** (80rem). *(User-chosen.)*
- **Responsive → desktop-first, graceful mobile** (two-column stacks, tables scroll/become card-lists under `md`) — no heavy mobile-polish investment this pass. *(User-chosen.)*
- **Scope → whole app, one branch.** *(User-chosen.)*
- **Wizards stay full-screen**; **document/reading viewers keep a comfortable reading measure** (not force-densified).
- **No rebrand** — same palette/type; this is layout/interaction only. Reuse existing primitives; add Modal/Drawer/ConfirmDialog/Table/DetailLayout.

## Resolved Questions

1. **App shell → widen to `max-w-7xl`** (80rem); reading viewers keep a narrower measure.
2. **Forms → Modal (short) + Drawer (long)**, not modal-universal; adds a `Drawer` primitive.
3. **Responsive → desktop-first, graceful degradation** (stacks/scrolls under `md`); no deep mobile polish this pass.

## Open Questions

1. **App shell width** — lists become full-width tables and details become two-column; is `MainLayout`'s `max-w-5xl` (64rem) still right, or widen it (e.g. `max-w-6xl`/`max-w-7xl`) so tables + two-column details breathe? (Affects every page.)
2. **Very long/complex forms** — the create-child form and IEP-create-then-upload flow are longer/multi-step. All-modal (with internal scroll), or do genuinely long forms use a **slide-over drawer** or stay a **dedicated sub-page**? (i.e. is "modal" truly universal, or "modal for short, drawer/page for long"?)
3. **Mobile** — two-column details and wide tables need a responsive story. Confirm: two-column collapses to stacked, tables become card-lists (or horizontal-scroll) under `md`. Any priority on mobile polish, or desktop-first for this pass?

## Out of Scope

Visual rebrand / new color or type language; backend/API changes; new product features; the marketing site. Reusing the established primitives — only the four new layout/interaction primitives are added.
