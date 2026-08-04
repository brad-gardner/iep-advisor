# UX Consistency & Professionalism Audit → Rework — Brainstorm

**Date:** 2026-07-01
**Status:** Draft
**Precedes:** `/sht:plan`

## Context

The owner feels the app is "disorganized, inconsistent, and unprofessional." Two research passes (repo inventory + external best-practices) converged on one diagnosis: **a real design system already exists but is under-constrained and under-enforced, and a few load-bearing primitives are missing.** This is a systematization problem, not a visual-design problem — every cited source (Refactoring UI, The Linear Method, Shopify Polaris, NN/g) says "inconsistent/unprofessional" is almost always *unconstrained values + multiple ways to do the same thing*, fixed by constraint and one component vocabulary.

**Target feel:** calm, trustworthy, understated SaaS (Linear/Stripe/Polaris school) with a touch of warmth — fundamentally a productivity tool. For a FERPA/student-PII product, visual polish and restraint are literally trust signals (NN/g: design quality is one of four credibility factors).

**Scope:** all authenticated surfaces (parent IEP/ETR/analysis, educator/district-admin org, student, platform admin), not just the pilot.

### Evidence of the problem (from repo inventory)
- **No shared `Spinner`** → 54 hand-rolled copies, sizes `h-3.5`–`h-10`, colors teal/amber/white.
- **No `PageHeader`/`PageLayout`** → each page rolls its own title + wrapper; 3+ competing H1 styles (bare `font-serif`, `text-2xl`, `text-[32px]`, auth pages use `<h2>`), container widths from `max-w-md` to `max-w-6xl` nested inside the shell.
- **No `EmptyState`** primitive → duplicated per feature.
- **No red/danger color scale** in the theme (only flat `brand.red`) → error styling falls back to raw `red-*`, inconsistently, even *inside* the Badge/Button/Notice primitives.
- 54 raw `<button>` and 14 raw form controls bypass the primitives; `Badge`/`Notice` render `info` and `success` identically.
- Two pages bypass the app shell entirely (setup wizard, onboarding).
- Role homes are thin (educator home ≈ a profile card); dev jargon surfaces in UI (e.g. raw `DistrictAdmin`).

## What We're Building

An **A+B hybrid**: foundations-first, migrated role-by-role starting with the pilot (district-admin/educator) surface.

1. **Foundations (breadth-first):**
   - Missing primitives: `Spinner`, `PageLayout`/`PageHeader` (title, optional breadcrumb, one primary-action slot, capped content width), `EmptyState` (guidance + action), and a proper `danger` color scale so error surfaces stop using raw `red-*`.
   - Token discipline: stop re-declaring `font-serif`/sizes (lean on the `index.css` type scale), constrain container widths and `space-y-*` to a small allowed set, one accent + neutral ramp, semantic colors for status only.
   - One component vocabulary: route all buttons through `Button` (add `size`/`loading`/icon props), all inputs through `Input`/`Select`/`Textarea`, de-duplicate `Badge`/`Notice` `info`/`success`.
   - A documented **state rule**: skeletons for full-page/region loads, spinner for isolated modules; guidance-oriented empty states; inline/banner errors with retry; explicit success feedback; confirm destructive actions.
   - Accessibility polish that also reads as professional: visible focus states (WCAG 2.4.11), gray-ramp contrast (4.5:1 / 3:1), one `h1` per page + logical heading order, accessible names for icon buttons and spinners (`role="status"`), never status-by-color-alone.

2. **Page migration (depth-first ordering):** move screens onto the shared shell + primitives, **starting with the district-admin/educator pilot surface**, then parent, student, and platform admin.

3. **Role IA rework:** purpose-built role homes that answer "what do I do next?" (not thin profile cards or data dumps — 3–6 meaningful modules each), task-grouped nav in the user's vocabulary, and a defined first-run path per role.

## Why This Approach

- The research's #1 lever ("constrain the system; one way to do each thing") is breadth-first infrastructure — so foundations go first.
- But the pilot is imminent for district-admin/educator, so migrating that surface first (B's ordering) makes the pilot screens land polished soonest while the primitives are reused everywhere after.
- Deferring the IA rework (role homes/nav) until the shell + primitives exist makes those product decisions cheaper to execute and easier to keep consistent.

## Key Decisions

- **New primitives to add:** `Spinner`, skeleton loaders, `PageLayout`/`PageHeader`, `EmptyState`, `Toast`, and a `danger` color scale (retire raw `red-*`).
- **Product name is "IEP Advisor"** — standardize all user-facing copy to it.
- **Loading:** build skeletons (full-page/region) + `Spinner` (isolated modules) together, with one documented rule.
- **Success feedback:** `Toast` for transient success; inline/banner for errors and anything requiring a decision.
- **Warmth via tone only** — microcopy + warm neutrals, no illustration/emoji.
- **Constraint over creation:** the theme is largely good — the work is *enforcement* (kill one-off sizes/widths/spacers), not a new visual language.
- **One vocabulary:** all buttons/inputs through primitives; fix `Badge`/`Notice` `info`≠`success`.
- **State rule:** skeleton-vs-spinner decided once and applied everywhere; every list/page gets designed empty/loading/error/success states.
- **Migration order:** pilot (district-admin/educator) → parent → student → platform admin.
- **Role homes:** each role lands on an operational "what next" home; no thin/empty dashboards.
- **Trust/terminology:** one term per concept, no dev jargon or raw role enums in UI; consistent product name.
- **Accessibility is in-scope**, treated as a professionalism signal, not a separate track.

## Resolved Questions

1. **Product name → "IEP Advisor"** (match the logo). Standardize every user-facing "IEP Assistant" reference to "IEP Advisor" app-wide.
2. **Success feedback → add a `Toast` primitive** for transient success, with inline/banner for critical/errors (Polaris split). New primitive in scope.
3. **Loading → skeletons + spinner rule, now.** Build skeleton loaders for full-page/region loads AND the canonical `Spinner`, with the documented skeleton-vs-spinner rule, in this effort.
4. **Warmth → tone only.** Supportive microcopy + slightly warm neutrals; no illustration/emoji. Keep institutional seriousness for a FERPA product.
5. **Migration order → pilot first.** District-admin/educator surface first, then parent, then student, then platform admin.

## Out of Scope

New product features or flows beyond what already exists; backend/API changes (this is a frontend consistency + IA rework); a full visual rebrand or new color language (the existing palette stays — the work is enforcing it); marketing/public site.
