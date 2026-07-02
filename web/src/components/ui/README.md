# Design system — usage guide

The shared UI vocabulary for IEP Advisor. **Use these primitives; don't hand-roll their patterns.** A CI guard (`npm run guard:ux`) enforces this on the migrated pilot surface (district-admin / educator / staff-invites); the rest of the app migrates in the fast-follow plan.

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

## Rules

- **State rule:** full-page/region load → `Skeleton`; in-place refresh → `Spinner`; empty → `EmptyState`; error → inline `Notice` with retry; success → `Toast`. Confirm destructive actions.
- **One `<h1>` per page** — via `PageHeader`. Don't add page-level `<h1>` in the body.
- **Forward `data-testid`** on any new primitive (`...rest`) — the e2e suite selects by testid; never rename/remove one during a migration.
- **Color tokens only** — `brand.teal` / `brand.slate` / `brand.amber` / `brand.danger`. No raw Tailwind `red-*`/`gray-*`.
- **Accessibility:** visible focus states, icon-only controls need `aria-label`, `prefers-reduced-motion` respected (Spinner/Skeleton/Toast), status never conveyed by color alone.
- **Terminology:** plain user vocabulary; map internal enums for display (e.g. `orgRoleLabel()`), never surface raw role constants.

## Tests

Primitives have Vitest + Testing Library tests (`*.test.tsx`) including a11y assertions. Run `npm test`; type-check tests with `npm run test:types`.
