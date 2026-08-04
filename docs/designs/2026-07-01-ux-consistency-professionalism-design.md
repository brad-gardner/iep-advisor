# Design Discussion: UX Consistency & Professionalism Rework

**Date:** 2026-07-01
**Feature:** App-wide frontend consistency + IA rework — build missing primitives, enforce one component vocabulary, standardize a page shell, and rework role homes/nav to a calm, trustworthy SaaS bar.
**Origin:** `docs/brainstorms/2026-07-01-ux-consistency-professionalism-brainstorm.md` (all 5 open questions resolved there)

## Current State

A real design system exists but is under-constrained and under-enforced; a few load-bearing primitives are missing.

- **Primitives** (`web/src/components/ui/`, 9 files): `Button` (variants primary/secondary/amber/ghost/danger; no size/loading/icon prop, no ref), `Card` (`accent?` only), `Input`/`Textarea`/`Select` (only three with `forwardRef`; no error/help/size props), `Badge` + `Notice` (both render `info` and `success` **identically**), `Tabs`, `ProgressDots`, `Logo`. **Missing entirely:** `Spinner`, `Skeleton`, `PageLayout`/`PageHeader`, `EmptyState`, `Toast`, `Modal`.
- **Styling mechanism:** every primitive uses a plain `Record<Variant, string>` map interpolated into a base string with a trailing `${className}` — **no `cva`/`clsx`/`tailwind-merge`** in the repo (`web/package.json`).
- **Theme** (`web/tailwind.config.js`): `brand.teal` (9 stops), `brand.slate` (9), `brand.amber` (7) are real scales; **`brand.red` is one flat hex `#B91C1C`** — so error surfaces mix `brand.red` (text) with raw Tailwind `red-50/200` (bg/border), even inside `Badge`/`Button`/`Notice`. Radii tokens exist (input/badge/button/card/modal). `index.css` sets global `h1/h2/h3` (Lora 600; 2rem/1.375rem/1.0625rem); no CSS-variable token layer.
- **Shell & routing:** `main-layout.tsx` wraps content in `max-w-5xl mx-auto px-4… py-8`. Landing via `roleHome()` (`role-home.ts`) + `RoleRoute`/`RoleHome` (`role-routing.tsx`). `sidebar.tsx` role-branches module-level nav arrays. Two pages bypass the shell (`district-setup-wizard`, `onboarding-flow`).
- **Per-page boilerplate:** every page repeats `<div className="space-y-6"><h1 className="font-serif">…` + a copy-pasted spinner (`animate-spin … border-b-2 border-brand-teal-500`, **54 instances**, varying sizes/colors) + inline empty/error blocks. Data-fetch is inconsistent (hand-rolled `useState/useEffect` in `district-staff-page` vs. a feature hook `useAllEtrs` in `etr-list-page`).
- **Tests:** **no component/unit test tooling in `/web`** (no Vitest/Jest/RTL/Storybook; no `*.test.tsx`). Only the top-level `e2e/` Playwright suite exists. Components carry `data-testid` hooks.
- **Evidence of inconsistency:** 54 raw spinners, 54 raw `<button>`, 14 raw form controls, 3+ competing H1 styles, container widths `max-w-md`…`max-w-6xl` nested in the shell, thin role homes (educator home ≈ a profile card), dev jargon in UI (raw `DistrictAdmin`), product name split ("IEP Advisor" logo vs "IEP Assistant" elsewhere).

## Patterns to Follow

- **Keep the existing styling convention:** `Record<Variant, string>` + template concatenation + `${className}` passthrough. New primitives match it (no new styling lib) — consistent with `[[feedback-minimal-dependencies]]`.
- **`forwardRef` + `...rest` spread** as in `input.tsx` for any new interactive primitive.
- **App shell:** reuse `MainLayout`'s container; the new `PageLayout`/`PageHeader` slots *inside* it (title/subtitle/breadcrumb/primary-action), replacing per-page `space-y-6`+`<h1>`.
- **Landing/IA:** extend `roleHome()`/`RoleRoute`/`sidebar.tsx` nav arrays — don't invent a new routing system.
- **Data-fetch:** prefer the feature-hook style (`useAllEtrs`: `{data, loading, error, refresh}`) over hand-rolled `useState/useEffect` when a page is migrated, so loading/empty/error map cleanly onto the new state primitives.
- **Icons:** `lucide-react` (already the only icon dep).

## Desired End State

1. **Theme:** `brand.red` becomes a full `danger` scale (50–800); every raw `red-*` retired.
2. **New primitives:** `Spinner`, `Skeleton`, `PageLayout`/`PageHeader`, `EmptyState`, `Toast` (+ provider/portal). `Button` gains `size` + `loading`; `Badge`/`Notice` `info`≠`success` fixed.
3. **One vocabulary enforced:** all buttons → `Button`, all inputs → `Input`/`Select`/`Textarea`, all loaders → `Spinner`/`Skeleton`, all empties → `EmptyState`, all page chrome → `PageLayout`.
4. **State rule applied everywhere:** skeleton for full-page/region loads, spinner for isolated modules; guidance-oriented empty states; inline/banner errors with retry; `Toast` for transient success; confirm destructive actions.
5. **Role IA:** each role lands on an operational "what next" home (3–6 modules), task-grouped nav in user vocabulary; no thin/empty dashboards.
6. **Trust polish:** product name standardized to **"IEP Advisor"**; one term per concept, no raw role enums/dev jargon; accessibility baseline (focus states per WCAG 2.4.11, gray-ramp contrast, one `h1`/page, icon/spinner labels, no status-by-color-alone).
7. **Migration order:** district-admin/educator (pilot) → parent → student → platform admin.

## Design Decisions

- **No new styling lib** — extend the existing template-string pattern; if class composition gets unwieldy, add a tiny in-repo `cn()` (hand-rolled join), not `tailwind-merge`. *(Open Q1)*
- **`Toast` built in-repo** (context + portal + `role="status"`), not a third-party lib — aligns with minimal-deps. *(Open Q2)*
- **Component test tooling:** recommend adding **Vitest + @testing-library/react**, scoped to the new primitives + a11y assertions (focus/role/aria), since primitives are pure and reused app-wide. *(Open Q3)*
- **`PageLayout` composes inside `MainLayout`** (doesn't replace it); pages opt in during migration, so unmigrated pages keep working — enables incremental, low-risk rollout.
- **Danger scale derived from `#B91C1C`** as the 600/700 anchor; generate 50–800 to match the teal/slate ramp shape.
- **IA rework is per-role and additive** — new home content + nav labels; existing routes/guards unchanged.
- **Verification leans on visual/manual + e2e**, since there's no component-test history; new Vitest covers primitives only.

## Resolved Questions

1. **Class helper → add a tiny hand-rolled `cn(...classes)` join** (no `tailwind-merge`). Tames the `Button` size×variant×loading matrix while staying minimal-deps.
2. **Toast → built in-repo** (context + portal + `role="status"`). No third-party toast lib.
3. **Test tooling → add Vitest + @testing-library/react**, scoped to the new primitives + a11y assertions.
4. **Scope → this plan = foundations + pilot-surface migration + pilot role IA.** Parent/student/platform-admin migration is an explicit fast-follow plan reusing these primitives.
5. **Setup-wizard / onboarding → stay full-bleed** (intentional first-run focus), but adopt the new primitives internally (Spinner/Button/EmptyState) for consistency. Not migrated onto `PageLayout`.

## Testing Strategy

- **Primitives:** Vitest + RTL (if adopted) — render/variant/prop tests and a11y assertions (`role="status"` on Spinner, focus-visible, `aria-label` on icon buttons, `info`≠`success`). Otherwise type-check/build + Storybook-less manual review.
- **Migrated pages:** the existing `e2e/` Playwright suite must stay green (selectors are `data-testid`, preserved through migration); add a couple of e2e checks for new toast/empty-state behavior on the pilot surface.
- **Visual/manual:** a per-phase screenshot pass against the calm-SaaS bar (spacing, one type scale, one spinner, consistent headers).
- **Accessibility:** axe/manual checks on migrated surfaces (focus states, contrast, heading order).
- **Consistency guard:** grep-based checks per phase (count remaining raw spinners / raw `<button>` / raw `red-*` / competing H1 styles) to measure the sweep's completeness.
