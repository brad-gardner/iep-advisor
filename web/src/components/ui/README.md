# Design system — usage guide

The shared UI vocabulary for IEP Advisor. **Use these primitives; don't hand-roll their patterns.** The whole authenticated app is migrated onto them. A CI guard (`npm run guard:ux`) enforces **zero raw spinners and zero raw `red-*`** app-wide (raw `<button>` is allowed only for native tabs/menus/switches/radios/disclosure-triggers/inline-links that have no `Button` equivalent).

## Primitives

| Need | Use | Notes |
|---|---|---|
| Page chrome | `PageLayout` / `PageHeader` | Owns the single `<h1>` (title). Slots: `subtitle`, `breadcrumb`, `actions` (primary top-right). Composes **inside** `MainLayout`. |
| Loading (full page/region) | `Skeleton` | Match final content dimensions to avoid layout shift. `aria-hidden`. |
| Loading (isolated module / in-place) | `Spinner` | `role="status"` + label. `size` (sm/md/lg), `tone` (`brand`/`current` — use `current` inside colored buttons). |
| Empty list/page | `EmptyState` | Guidance, not blank: `icon` + `title` + `description` + optional `action`. |
| Buttons | `Button` | Variants primary/secondary/amber/ghost/danger; `size` (sm/md/lg); `loading` (disables + `aria-busy` + width-reserving spinner). |
| Inputs | `Input` / `Select` / `Textarea` | `forwardRef`; optional `label`. |
| Status chips / inline messages | `Badge` / `Notice` | `success`(teal) ≠ `info`(slate); `error` uses `brand-danger`. Status is always icon+text, never color alone. |
| Transient success feedback | `useToast().show({ message, variant })` | `ToastProvider` is mounted once at the app root. Success → toast; errors/decisions → inline `Notice`. |
| Class composition | `cn(...)` from `@/lib/cn` | Plain join, **not** Tailwind-merge — pass the intended final class; later class wins by source order. |
| Short form / focused dialog | `Modal` | Native `<dialog>` (`open`/`onClose`/`title`/`size`/`footer`). Hosts ≤~5-field forms opened from a `PageHeader` action. Unmounts children when closed. |
| Long / multi-section form | `Drawer` | Right-anchored native `<dialog>`, same contract as `Modal`. Keeps list context. |
| Destructive confirmation | `ConfirmDialog` | `role="alertdialog"`, focuses **Cancel**, action-named confirm button, optional `error` slot (server failure stays in the dialog). Replaces `window.confirm()`. |
| Data list | `Table` | Native `<table>`, sortable `<th>` headers (`aria-sort`), kebab `rowActions`, `rowHref` (accessible whole-row link), skeleton/`empty` states, `hideBelow` per-column. Client-sort ≤ `CLIENT_SORT_ROW_CEILING`. |
| Row / dropdown actions | `Menu` | APG menu-button, portals to `<body>` (never clipped). Backs the Table kebab. |
| Two-column detail page | `DetailLayout` | `main` + `sidebar` (status/metadata/quick-actions); **main first in source order**; stacks under `md`. |
| Reading-width cap | `ReadingColumn` | Caps long-form/document/reading blocks at ~65ch (`max-w-prose`) inside the wide `max-w-7xl` shell. Wrap IEP/ETR/PDF viewers. |

## Rules

- **State rule:** full-page/region load → `Skeleton`; in-place refresh → `Spinner`; empty → `EmptyState`; error → inline `Notice` with retry; success → `Toast`. Confirm destructive actions with `ConfirmDialog`.
- **One `<h1>` per page** — via `PageHeader`. Don't add page-level `<h1>` in the body.
- **Forward `data-testid`** on any new primitive (`...rest`) — the e2e suite selects by testid; never rename/remove one during a migration.
- **Color tokens only** — `brand.teal` / `brand.slate` / `brand.amber` / `brand.danger`. No raw Tailwind `red-*`/`gray-*`.
- **Accessibility:** visible focus states, icon-only controls need `aria-label`, `prefers-reduced-motion` respected (Spinner/Skeleton/Toast/overlays), status never conveyed by color alone.
- **Terminology:** plain user vocabulary; map internal enums for display (e.g. `orgRoleLabel()`), never surface raw role constants.

## Page composition (layout & interaction)

- **Lists are `Table`s** — not `<ul>`-of-`<Card>`. Give each a `label`, `rowKey`, sortable columns (`sortValue`), a `rowHref` for navigation, and a kebab `rowActions` for per-row verbs. Reserve the `empty`/loading states.
- **Detail pages are `DetailLayout`** — primary content in `main`, status/metadata/quick-actions in `sidebar`.
- **Create/edit forms live in a `Modal` (short) or `Drawer` (long)** opened from a `PageHeader`/section action — not inline on the page. Forms expose a controlled `onSubmit → Promise<{success,error}>`; the **host closes only on resolved success** (then `Toast`), and a failure keeps the form rendered with its inline `Notice`. Shared forms take an `embedded` prop to drop their self-`Card`+heading when hosted in an overlay.
- **Never `window.confirm()`** — use `ConfirmDialog`. The `guard:ux` check enforces this app-wide.
- **Width is per-region** — the shell is `max-w-7xl`; data-dense pages fill it. Wrap reading/document/viewer blocks (IEP/ETR/PDF) in `ReadingColumn` (~65ch) so they stay legible.
- **Wizards** (onboarding, district-setup) stay full-screen — they are not overlay-hosted.

## Tests

Primitives have Vitest + Testing Library tests (`*.test.tsx`) including a11y assertions. Run `npm test`; type-check tests with `npm run test:types`.
