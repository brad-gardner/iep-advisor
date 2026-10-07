---
module: "i18n Phase 5 (staff/admin role-split English, lazy route chunks)"
date: "2026-10-07"
problem_type: logic_error
component: frontend_react
symptoms:
  - "Bundling all English pushed the main chunk to 410 kB gzip against a 418 kB budget, with staff pages still to come"
  - "After splitting staff English into lazy chunks, a parent page's PDF button showed raw keys ('authoredPdfDownload.download') because it used a staff namespace"
  - "District-admin compliance page threw missing-key errors: a shared educator component's namespace was registered only by the staff chunk"
  - "Spanish staff saw English obligation chips on the home page: the helper's namespace was never requested there, so i18next fell back to English"
  - "The first chunk-reload guard looped forever when a chunk was genuinely missing, and the route error boundary stayed stuck after navigation"
root_cause: logic_error
resolution_type: code_fix
severity: high
tags: [i18n, code-splitting, lazy-routes, react-i18next, vite, preload-error, error-boundary, bundle-size, namespaces]
---

# Troubleshooting: role-split English locales and lazy-chunk boundaries

## Problem

Phase 2 bundled all English eagerly, to avoid raw-key flashes. At the end of Phase 4 the main chunk was 410 kB gzip against a 418 kB budget, and the staff pages were still to come.

Phase 5 of `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` therefore split English by audience:
- Shell and parent namespaces stay eager.
- Staff and admin namespaces move to `locales/{en,es}/staff/`.
- Those namespaces are registered when the lazy staff, district-admin or platform-admin route chunk loads.

The main chunk dropped to 308 kB gzip. The split also created new boundaries, and every problem found came from crossing one of them.

## Environment

- React 19, React Router 7 (declarative `<Routes>`), i18next 26 / react-i18next 17 with `useSuspense: false`.
- Vite 7 (`import.meta.glob`, `vite:preloadError`), @sentry/react 10.
- Date: 2026-10-07.

## Symptoms and causes

1. **An eager page used a staff namespace.** `AuthoredPdfDownload` sits in a staff folder but renders on the parent `ParentAuthoredVersionPage`, which is in the main chunk.
   - English is never registered there, so the backend's deliberate "English must be bundled or registered" throw fired.
   - The button then showed raw keys permanently, because i18next marks the failed namespace as loaded.
2. **Cross-area staff components.** The district-admin chunk rendered the educator school filter, but only the staff chunk registered the `educator` namespace.
3. **A helper outside any subscription.** `obligationStatusLabel()` calls `i18n.t`. On the staff home, no component requested the `obligations` namespace.
   - The Spanish bundle was never fetched, so Spanish users got the English fallback.
   - It turned Spanish only after they happened to visit a page that did request it.
4. **Lazy routes added a chunk-load failure mode.**
   - The first guard cleared its flag on `window.load`. Lazy imports start after auth, which is *after* `load`, so a chunk that stayed missing reloaded the page forever.
   - Background imports (the editor warm-up) also triggered reloads.
   - The route error boundary stayed in its error state across sidebar navigation, because `MainLayout` is reused between routes.
5. **The first boundary guard test missed multi-line imports.** The regex didn't span newlines, so several eager files (e.g. `auth-context.tsx`) went unwalked.

## Solution

- **Registration:** `app/lazy-routes/staff-locales.ts` registers every `locales/en/staff/*.json` with one eager glob plus `registerEnglishNamespace`.
  - All three area barrels import it, since district and platform admins are staff too.
  - A new namespace needs only its two JSON files and a `types.d.ts` line.
- **Eager-vs-staff placement:** a component reachable from eager code uses an eager namespace. For example, PDF and snapshot strings live in `document-authoring-shared`.
  - `lib/i18n/staff-namespace-boundary.test.ts` walks the static import graph from `main.tsx`, following multi-line imports but not dynamic `import()`.
  - It fails if any eagerly reachable file references a staff namespace, with one narrow allowlist entry.
- **Helpers:** every component that renders a plain-function label helper lists the helper's namespace in its own `useTranslation`. That both loads the Spanish bundle and re-renders when it arrives. Helpers pass `defaultValue: raw`.
- **Chunk failures:**
  - `vite:preloadError` triggers a reload guarded by a 30 s timestamp cooldown (storage access is in try/catch).
  - Background imports mark themselves, so their failures never reload the page.
  - The route error boundary uses a module-level fallback component that resets when the pathname changes.
- **CI:** `npm run check:bundle` fails the build above 418 kB gzip and warns above 405 kB.
- **API (same phase):**
  - The staff services set `ServiceErrorKind`, and English statuses are pinned to match main.
  - The no-brief case is deliberately changed to 404, so the Generate state is reachable.
  - AI suggestions that get inserted into IEP documents stay English (the legal record's language). Assist chat and meeting briefs follow the staff member's language.

## Verification

- **Web:** `npx vitest run`: 1519 passed. `tsc -b` and `test:types` are clean, ESLint stays at 36, and the main chunk is 308.32 kB gzip.
- **API:** `dotnet test`: 1694 passed.
- **Migration:** `AddMeetingBriefLanguage` was applied to QA before the merge.
- **Review:** 3 passes; P1 went 3 → 1 → 0 and P2 went 12 → 4 → 0.
- **Not verified:**
  - a real browser with a deleted chunk after a deploy;
  - a native-speaker review of the Spanish.
- **P3 follow-ups:** todos/249.

## Prevention

- When you move resources into lazy chunks, find every component reachable from eager code that uses them, and keep an automated import-graph guard.
- Register resources per audience, not per feature, when features render across areas.
- A plain `i18n.t` helper doesn't load anything by itself. The component that renders it must subscribe to the helper's namespace.
- **Reload-on-chunk-error guards:**
  - use a time window, never "clear on load";
  - ignore background imports;
  - test the event order that real apps produce (load fires before auth, then the import fails).
- Error boundaries inside layouts that persist across routes must reset on navigation. A function `fallback` has to be a stable reference.

## Related

- `docs/solutions/logic-errors/2026-10-06-i18n-status-codes-from-localized-text-and-lazy-fallback-language.md` (why English became eager in the first place)
- `docs/i18n/README.md` ("Staff and admin namespaces", "Staff namespaces across areas")
- `docs/plans/2026-10-06-001-feat-multilingual-english-spanish-plan.md` ("Decisions added during implementation")
