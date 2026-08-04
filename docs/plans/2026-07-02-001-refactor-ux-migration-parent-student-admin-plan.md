---
title: "refactor: UX migration — parent, student & platform-admin surfaces (fast-follow)"
type: refactor
date: 2026-07-02
status: completed
origin: docs/plans/2026-07-01-002-refactor-ux-consistency-professionalism-plan.md
slicing_approach: vertical
---

# refactor: UX Migration — Remaining Surfaces (fast-follow)

## Problem Statement

The design-system foundation shipped in `2026-07-01-002-refactor-ux-consistency-professionalism-plan.md` (primitives: `cn`, `Spinner`, `Skeleton`, `EmptyState`, `PageLayout`/`PageHeader`, `Toast`, `Button` size/loading, `Badge`/`Notice` fixes, `brand.danger` scale; Vitest layer). Only the **district-admin/educator/staff-invites pilot surface** was migrated onto it. This plan migrates the remaining authenticated surfaces onto the same primitives — **no new primitives should be needed** (that was the foundation's success criterion).

## Scope (app-wide baseline still to migrate)

From `npm run guard:ux` app-wide reference: **~58 raw spinners, ~55 raw `<button>`, ~60 raw red-*/brand-red** live outside the pilot dirs. Feature folders to migrate (by size):

- **Parent core (largest):** `iep-authoring` (educator authoring), `etr-documents`, `iep-documents`, `analysis`, `children`, `iep-versions`, `progress-reports`, `meeting-prep`, `iep-comparison`, `advocacy-goals`, `knowledge-base`, `sharing`.
- **Student:** `student`.
- **Platform admin:** `admin`.
- **Cross-cutting:** `auth` (login/register/reset heading consistency — currently `<h2>` centered), `subscription`, `onboarding` (full-bleed — adopt primitives internally, keep full-bleed like the setup wizard).

## Proposed Phases (vertical, per surface)

1. **Parent dashboard + children + documents shell** — migrate `/dashboard`, `children`, `iep-documents`, `etr-documents` list/viewer pages onto `PageLayout` + primitives + state rule + toasts; parent role home = operational "what next".
2. **Parent analysis / versions / comparison / progress-reports / meeting-prep / advocacy-goals / knowledge-base** — migrate the deep parent tools; document viewers keep their `text-[32px]` title but via `PageHeader`.
3. **Student surface** — `/student` home + accept-invite; operational student home.
4. **Platform admin** — `/admin` dashboard + users; align its `text-2xl`/`max-w-*` deviations onto `PageLayout`.
5. **Auth + onboarding polish** — auth pages heading consistency; onboarding stays full-bleed but adopts primitives; extend `guard:ux` to include the newly migrated dirs and drive the app-wide baseline toward zero.

## Invariants (carry from the foundation plan)

- Preserve every `data-testid`; keep the e2e suite green per phase (grep `e2e/` for text/role selectors before renaming copy).
- Shared-primitive changes are already done — this plan only consumes them.
- Reuse `orgRoleLabel()`-style display mappings; no raw enums in UI.
- Per-phase: `npm test` + `npm run test:types` + `npm run build` green; `npm run guard:ux` extended and green for migrated dirs.

## Out of Scope

New primitives (foundation should suffice — if a surface genuinely needs one, e.g. `Modal`/`Table`, add it as a small sub-task and note it); backend/API; marketing site.

## Outcome (2026-07-02) — COMPLETE

Migrated the **entire authenticated app** in one branch `refactor/ux-migration-remaining` (7 commits off main, ~104 files) via parallel workers over disjoint feature dirs, in 3 batches:
1. Parent core (children, iep-documents, etr-documents, parent dashboard) · student + subscription · platform-admin
2. Parent tools (analysis, iep-versions, iep-comparison, progress-reports, meeting-prep, advocacy-goals, knowledge-base, sharing) · auth + onboarding
3. **iep-authoring workspace + child-links + the 2 auth-guard spinners** (this dir was missed in the initial batching and picked up as a straggler)

- **Foundation held: no new primitives needed** (the success criterion). Every surface uses the existing `PageLayout`/`Spinner`/`Skeleton`/`EmptyState`/`Button`/`Badge`/`Notice`/`Toast`.
- **App-wide result: 0 raw `animate-spin`, 0 raw `red-*`/`brand-red`** (outside `ui/`). `guard:ux` extended app-wide to enforce these two invariants; raw `<button>` (39, in tabs/menus/switches/radios/disclosure-triggers/inline-links — no `Button` equivalent) is informational.
- Full-bleed flows kept full-bleed (auth cards, onboarding, iep-authoring workspace, accept-link) — adopted primitives internally, not `PageLayout`.
- Success toasts wired on discrete mutations across all surfaces (not autosave/keystroke saves).
- **72 unit tests pass, `test:types` clean, `build` green, lint baseline held (37), `guard:ux` green.** Every `data-testid` preserved; per-surface workers each ran react-reviewer (authoring + async surfaces also react-async-reviewer).

**Remaining validation (ship-notes, stack-dependent):** run the `e2e/` Playwright suite (testids + the few text selectors were preserved) and a visual pass of the newly-migrated surfaces. `Select` primitive has a minor pre-existing type note (`InputHTMLAttributes<HTMLSelectElement>` cast) — follow-up cleanup, not blocking.
