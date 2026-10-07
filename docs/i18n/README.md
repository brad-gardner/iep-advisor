# Multilingual site (i18n)

English and Spanish ship together. This page explains how to add a translation
key, how the lint ratchet works, how the parity test is enforced, and which
namespaces are converted so far. For Spanish wording rules (tone, formality,
special-education terminology), see [`glossary-es.md`](./glossary-es.md).

## How it's built

- **Library:** `i18next` + `react-i18next`, with `i18next-resources-to-backend`
  for lazy-loading Spanish. No detector plugin: `lib/i18n/index.ts` resolves
  the initial language itself (`detectInitialLanguage()`, synchronous) and
  passes it as `lng` at `init()`; a signed-in user's confirmed preference and
  a `?lang=` visit are applied afterward via `i18n.changeLanguage` directly
  (`AuthProvider`, `useLanguageQueryParam`).
- **Resources (phase 2 review — all English is bundled, not just the shell):**
  `web/src/locales/{en,es}/<namespace>.json`. Every `en/*.json` file is
  bundled EAGERLY — `index.ts`'s `resources.en` is built from
  `import.meta.glob('/src/locales/en/*.json', { eager: true })`, so a new
  namespace's English is picked up automatically with no import to add. Only
  Spanish stays lazy, per namespace, via the same `resourcesToBackend` plugin
  (`index.ts`'s `esLoaders`, globbing `locales/es/**/*.json` — English never
  touches this backend). This replaced phase 1's design, where only the two
  shell namespaces (`common`, `auth`) were bundled and every other namespace
  was lazy in BOTH languages: that showed a raw `ns:key` flash on a feature
  page's first paint in English too, and — worse — got permanently STUCK
  showing raw keys if the lazy English chunk failed to load (the
  `failedLoading` handler's English fallback is useless once English is
  itself the thing that failed to load). See the plan's "Decisions added
  during implementation." **A staff/admin-only namespace (phase 5) is the
  one exception to "every `en/*.json` file is bundled eagerly"** — its
  English lives under `locales/en/staff/` instead, specifically so it's
  excluded from eager bundling and ships with its own route chunk instead.
  See "Staff and admin namespaces" below before adding one.
- **Namespaces:** one per feature folder, plus `common` for shared chrome
  (layouts, `components/ui/*`, enum-label helpers, generic errors). A page
  reads from its own feature namespace and `common`. **`index.ts`'s `ns`
  option still only ever lists the shell namespaces (`common`, `auth`)** —
  those two are the only ones that must be ready, synchronously, before the
  shell (layouts, the switcher, auth pages) renders at all. Every other
  namespace is **not** added there, even though its ENGLISH data now sits in
  `resources` right alongside the shell namespaces': `useTranslation('<that
  namespace>')` sees it as already loaded (no network call) the instant a
  component first asks for it, in English, regardless of whether it's in
  `ns`. Only its SPANISH data is genuinely lazy, fetched on that same first
  call. Adding a namespace to `ns` would force Spanish to eagerly fetch every
  namespace's chunk up front, defeating the point of lazy-loading Spanish at
  all.
- **Typed keys (phase 2 review — every namespace is strict now, not just
  `common`/`auth`):** `web/src/lib/i18n/types.d.ts` declares `EnResources`
  from one `import type` per `en/*.json` namespace file — type-only, so it
  costs nothing at runtime — and sets `CustomTypeOptions.resources` to it
  directly. A dynamic `import.meta.glob` can't itself produce this: Vite
  types every glob match with the same generic shape, with no literal
  per-file key or per-file JSON shape (see
  `node_modules/vite/types/importGlob.d.ts`), so there's no way to derive a
  strict, per-namespace key union from `index.ts`'s runtime `resources.en`
  alone. `EnResources` is the parallel, hand-maintained stand-in: it must
  name the same namespaces `resources.en` does (`index.test.ts`/
  `locale-parity.test.ts` cover the files on disk), and registering a new
  namespace needs exactly one `import type` line there. Forgetting it isn't
  silent — the first `useTranslation('<that namespace>')` call for it fails
  to compile, since the namespace is simply unknown to `CustomTypeOptions`,
  not loosely typed. This means `t('common:unknownKey')`,
  `t('children:unknownKey')`, etc. are ALL now `tsc` errors, for every
  namespace — phase 1 only caught this for `common`/`auth`.
  **Gotcha — reaching another namespace from a single-namespace hook:**
  `useTranslation('home')`'s `t` only type-checks keys from `home` itself (or
  `ns:key` for a namespace in the SAME array) — `t('common:ui.tryAgain')`
  from a single-namespace `home` hook is a `tsc` error now that `home` is
  strictly typed (phase 1 this compiled, loosely, because every feature
  namespace was `Record<string, string>`). Pass an array instead:
  `useTranslation(['home', 'common'])`.
- **Language resolution** (`web/src/lib/i18n/detect.ts`), for what a page
  *shows*, in order:
  1. the signed-in session's **cached** account language — the
     `preferredLanguage` on the last `/me` response `lib/auth` stored in
     `localStorage`, read synchronously so a page *reload* doesn't flash the
     pre-login/browser language first;
  2. the visitor's **explicit** pre-login choice — the switcher, or a
     `?lang=en|es` query param honored once by `useLanguageQueryParam`,
     while signed out (`getPreLoginLanguage`). A signed-in visitor following
     a `?lang=` link (e.g. an invite opened while signed in as someone else)
     still sees that language for display, but it is NEVER written to this
     key (phase 2 review) — writing it would let a signed-in account's
     incidental link click decide a *different*, later visitor's backfill on
     a shared device, which is exactly what this key must never do (see the
     module doc comment on `detect.ts`);
  3. the **sign-out display carry-over** — whatever language was active when
     the previous session signed out (`getLastDisplayLanguage`), so the login
     page doesn't flash back to the browser's language the instant someone
     signs out;
  4. the browser's `navigator.languages` (`es-*` → `es`);
  5. `en`.

  Signals 2 and 3 above are deliberately two different `localStorage` keys,
  not one, and **only the explicit choice (2) may ever decide a null-
  preference backfill** (the first sign-in for an account with no saved
  language yet — see below). The carry-over (3) is display-only: on a
  shared/kiosk device, the language the *previous* user left active says
  nothing about what the *next* signed-in account should use, so
  `AuthProvider.syncLanguagePreference`'s backfill resolves with
  `getPreLoginLanguage() ?? detectBrowserLanguage()` — **never**
  `i18n.language` and **never** `getLastDisplayLanguage()`. Conflating the
  two was a real bug (a signed-out carry-over silently becoming a stranger's
  permanent account preference); see the doc comments on `detect.ts` and
  `AuthProvider.syncLanguagePreference` for the full reasoning.

  A signed-in user's language is *confirmed* separately, once `/me`
  resolves: `AuthProvider.loadUser`/`refreshUser` call `syncLanguagePreference`
  with the generation captured before that fetch started, so a stale response
  (one that resolves after a newer explicit switch) can never revert the
  language or persist a now-outdated choice — see the doc comments on
  `AuthProvider.setLanguage`/`languageGenerationRef` for the full race. An
  explicit `setLanguage` call also always awaits any backfill PUT already in
  flight (`pendingBackfillRef`) before sending its own, so the visitor's own
  choice is guaranteed to be the last write the server sees. Both
  `AuthProvider.loadUser`/`refreshUser`/`setLanguage` additionally capture
  the signed-in token *before* their own fetch/PUT starts and compare it
  against the current one once the response lands: if `logout()` ran in
  between, the token is gone, and the response is discarded rather than
  resurrecting a signed-in-looking user with no token (phase 2 review;
  todos/247). `logout()` also clears the explicit pre-login key
  defensively, so this departing session can never leave a value there for
  the next, possibly anonymous, visitor to be backfilled with.
- **Switching:** `web/src/lib/i18n/language-switcher.tsx` — the one
  `LanguageSwitcher` component, in the auth layout footer, the sidebar footer,
  and the Profile page's Language field (which also passes a `hint`).
  Switching updates `<html lang>` immediately and persists the choice
  (account PUT when signed in, the explicit pre-login key otherwise — never
  the sign-out carry-over key). Signing out writes the active language to the
  sign-out carry-over key (not the pre-login key), so the login page stays in
  that language instead of falling back to the browser; signing in clears
  that carry-over (its job is done) without touching the explicit pre-login
  key.
- **Formatting:** `web/src/lib/i18n/format.ts` (`getActiveLanguage`) plus
  `lib/format-date.ts` and `lib/relative-time.ts`, all keyed on the active
  i18next language rather than a hard-coded locale. A *computational* helper
  that re-parses its own `Intl`-formatted output (e.g.
  `features/meetings/lib/meeting-time.ts`'s UTC/timezone conversions) stays
  fixed to `en-US` regardless of the active language — only the
  *display*-facing formatters in that file follow it. Pages not yet
  converted in this phase may still show a mixed-language date (an
  unconverted page's own hard-coded `'en-US'`/`toLocaleDateString()` call);
  that's accepted until that page's phase converts it, not a Phase 1 bug.
- **`orgRoleLabel` (`web/src/lib/org-role-label.ts`) is already translated**
  via `common:orgRole.*`, independent of whether the *page* calling it has
  converted yet. `features/home/pages/staff-home-page.tsx` converted in
  phase 2; the remaining callers haven't converted their own surrounding text:
  `features/district-admin/components/dashboard-invites-tile.tsx`,
  `features/staff-invites/pages/district-staff-page.tsx`,
  `features/educator/components/team/add-team-member-form.tsx`,
  `features/educator/components/roster/assign-case-manager-modal.tsx`, and
  `features/educator/components/team/team-member-row.tsx`. Expect a
  mixed-language page (a translated role label inside otherwise-English
  surrounding text) from these until their own phase converts them — not a
  Phase 1 bug.
- **Display-label helpers follow `orgRoleLabel`'s shape**: a stored value
  (English, the source of truth — never translated) maps to a translated
  label for DISPLAY ONLY, via a plain function that calls `i18n.t` directly
  (not `useTranslation`, since these are called from render bodies and plain
  functions alike) with a sensible fallback for an unrecognized value.
  `web/src/lib/grade-level-label.ts` (`common:gradeLevel.*`) and
  `web/src/lib/disability-category-label.ts` (`common:disabilityCategory.*`,
  keyed by the IDEA category CODE rather than the stored label text) cover
  `features/children/lib/child-profile-options.ts`'s dropdown values and
  everywhere `ChildProfile.gradeLevel`/`disabilityCategory` are displayed —
  each NORMALIZES the raw stored value first (`normalizeGradeLevel`/
  `normalizeDisabilityCategory`, the same mapping the child profile form uses
  to pick a matching `<option>`) before looking up its translation, so a
  legacy/raw value (`"5"`, `"K"`, `"PK"`, an IDEA code like `"SLD"`, any case)
  resolves to the same translation its canonical form would, rather than
  falling through to the untranslated pass-through (phase 2 review).
  `web/src/lib/invite-status-label.ts` (`common:inviteStatus.status.*`)
  covers a meeting invite's RSVP status (Pending/Accepted/Declined/
  Tentative) the same way. `features/sharing/lib/role-label.ts`
  (`sharing:role.*`) covers the child-access share role (`owner`/`viewer`/
  `collaborator`) — written as a `switch` over literal keys rather than a
  dynamic one.
- **Where a display-label helper (or any small i18n helper) lives:** a
  helper with exactly one feature's callers lives in that feature's own
  `features/<feature>/lib/` (e.g. `features/sharing/lib/role-label.ts`,
  `features/educator/lib/student-enum-labels.ts`,
  `features/document-authoring/lib/owner-display.ts`); a helper with
  callers across more than one feature lives in the shared `web/src/lib/`
  instead (e.g. `grade-level-label.ts`, `invite-status-label.ts`,
  `meeting-labels.ts`, `obligation-label.ts`, `family-contact-label.ts`,
  `evaluation-case-label.ts` — each backs at least one namespace/feature
  other than the one it's named after). Moving a helper from
  `features/<f>/lib` to `web/src/lib` (or the reverse) the moment a second
  feature needs it is a one-file rename, not a design decision to agonize
  over — but a brand-new helper should start in the narrower location
  unless it's already known to be cross-feature.
- **DB-authored content stays English, with a note**: `features/knowledge-base`
  converts page chrome (search, category tabs, the disclaimer) but not an
  article's own `title`/`content`/`tags`/`state` (district/DB content, like
  `orgRoleLabel`'s "stored value" — translating it is the plan's own
  knowledge-base-articles follow-up, not this phase). Each entry card shows a
  small `knowledge-base:entryCard.availableInEnglish` note when
  `i18n.resolvedLanguage === 'es'`, so a Spanish reader is told plainly
  rather than left to assume the article itself was translated, and marks
  the title/content themselves `lang="en"` whenever the UI isn't English
  (phase 2 review) — same reasoning as `<html lang>` for the page as a
  whole, so assistive tech knows this one piece of text is a different
  language than its surroundings.
- **Generic "loading…"/"something went wrong" text is `common:ui.loading`/
  `common:ui.genericError`, not a new per-feature key** (phase 2 review):
  several namespaces had independently drafted their own near-duplicate of
  each (`home:parentPage.loading`, `children:form.genericError`, …) — now
  consolidated onto the one pair in `common`. A message that's actually
  MORE specific than "something went wrong" (e.g. `onboarding:child.
  genericError`, "An error occurred creating the profile") still gets its
  own key; only the genuinely generic, feature-agnostic ones are
  consolidated.
- **An effect that fetches on mount never has `t` in its dependency array**
  (phase 2 review: it did in several places — `use-home.ts`, `use-children.ts`,
  `AcceptLinkPage`, `UpcomingMeetingCard`, `NotificationsPage`,
  `AdminNotificationFailuresPage` — before this). `t`'s identity changes on
  every language switch, so including it re-runs the whole fetch just
  because the user changed languages — wasteful, and racy with whatever
  else that effect does. Store an error FLAG or CODE in state instead of a
  pre-translated string (e.g. `{ kind: 'server'; message: string } | { kind:
  'generic' }` — a server-provided message is already resolved text and
  shown as-is; the generic case is translated), and translate it at RENDER
  time, where the current `t` is always the current language. See
  `src/features/student/components/student-accept-invite-page.tsx` (phase
  1) for the original of this pattern, or any of the files above for its
  phase 2 fix. An effect using this pattern typically also wants an `active`
  (or a ref-backed equivalent) guard so a superseded/unmounted call's
  response is never applied after the fact.

## Staff and admin namespaces (phase 5 foundation)

Everything above still describes the shell and parent/family namespaces.
Starting phase 5, a namespace that belongs only to staff (`/educator/*`),
district-admin (`/educator/admin/*`) or platform-admin (`/admin/*`) pages
follows a DIFFERENT rule for its ENGLISH: it ships with the route code that
uses it, not in the main chunk. Spanish is unaffected — it's lazy exactly
the same way as every other namespace.

- **Where the files live:** `web/src/locales/en/staff/<ns>.json` and
  `web/src/locales/es/staff/<ns>.json` (both languages under `staff/`, even
  though only English's location actually matters for eagerness — see
  below). `educator`, `evaluation`, `family-contact`, `meeting-brief`,
  `meetings-staff`, `obligations` and `document-authoring` are wired this
  way so far; `features/district-admin`, `features/staff-invites`,
  `features/roster-import`, `features/calendar`, `features/contributions`,
  `features/exports` and `features/admin` still convert in a later phase.
- **Why ENGLISH needs a different mechanism:** `lib/i18n/index.ts`'s eager
  `enModules` glob (`/src/locales/en/*.json`) matches only DIRECT children of
  `locales/en/` — `locales/en/staff/educator.json` never matches it, so it
  never enters `resources`/the main chunk, automatically, just by living
  under `staff/`. Nothing needs to exclude it; the directory choice IS the
  exclusion.
- **How English actually loads, then:** one shared module,
  `web/src/app/lazy-routes/staff-locales.ts`, eagerly globs EVERY
  `locales/en/staff/*.json` file (`import.meta.glob('/src/locales/en/staff/*.json',
  { eager: true })`) and calls `registerEnglishNamespace(ns, json)`
  (`lib/i18n/index.ts` — `i18next.addResourceBundle('en', ns, json, true,
  true)`) once per file, keyed by its filename, at module top level. That
  one module is imported, for its side effect only, at the top of all THREE
  lazy area barrels (`staff-routes.tsx`, `district-admin-routes.tsx`,
  `platform-admin-routes.tsx`) — so every staff namespace's English
  registers the instant whichever area's chunk JS evaluates, strictly
  before any page component from that chunk can render (ES module
  evaluation order), and the main chunk never pays for any of it (the glob
  being "eager" only means eager WITHIN that already-lazy chunk — see
  "Staff namespaces across areas" below and `staff-locales.ts`'s own doc
  comment for the full reasoning). A brand-new staff namespace needs no new
  line here — dropping its `en/staff/<ns>.json` file on disk is enough; see
  `lib/i18n/index.ts`'s `registerEnglishNamespace` doc comment for why a
  `language !== 'es'` request ever reaching the Spanish-only backend throws
  loudly instead of silently serving the wrong language.
- **Spanish is unchanged in spirit:** `index.ts`'s `esLoaders` globs
  `/src/locales/es/**/*.json` (recursive — covers `es/staff/*.json` too) and
  keys its loader map by NAMESPACE NAME (the filename, regardless of
  subdirectory), not by directory, so a staff namespace's Spanish lazy-loads
  through the exact same backend as a parent/shell one's, the first time a
  component calls `useTranslation('<staff ns>')` while Spanish is active.
- **Typed keys follow the same pattern as every other namespace**
  (`lib/i18n/types.d.ts`): add one `import type` line for the staff
  namespace's `en/staff/<ns>.json` file and list it in `EnResources` — this
  costs nothing at runtime (erased by `tsc`) regardless of where the
  runtime English data actually lives.
- **Route splitting:** the pages that use a staff/admin namespace are
  themselves `React.lazy` route chunks (`app/routes.tsx`), grouped by area —
  one barrel module per area (`app/lazy-routes/staff-routes.tsx`,
  `district-admin-routes.tsx`, `platform-admin-routes.tsx`) re-exporting
  every page in that area, so every `React.lazy(() =>
  import('.../staff-routes').then(...))` call across many routes still
  resolves to ONE chunk rather than one per page. Each `Route`'s `element`
  wraps the lazy page in `<Suspense fallback={lazyRouteFallback}>` (the same
  in-page `justify-center py-12` spinner treatment used elsewhere, reusing
  `common:ui.loading` rather than a new key). A shared component rendered
  from a PARENT/family page must never import from a staff-only namespace —
  only a staff/district-admin/platform-admin page itself may call
  `useTranslation('<staff ns>')`.
- **Naming:** a staff/admin namespace's name must be globally unique —
  `esLoaders`/`enModules`-adjacent typing key by namespace NAME alone, not
  by directory, so `locales/en/staff/home.json` would collide with the
  existing parent `home` namespace. Pick a name no existing namespace (staff
  or parent) already uses. **Convention for a new eager/staff PAIR covering
  the same feature:** name the eager one `<feature>` and the staff one
  `<feature>-staff` — e.g. `features/meetings` ships both the eager
  `meetings` namespace (the parent-facing RSVP page) and the staff-only
  `meetings-staff` one (everything else in that feature), never two
  unrelated-looking names. Only add the `-staff` suffix when an eager
  namespace of the plain name already exists or is expected; a feature with
  no eager surface at all (e.g. `educator`, `obligations`) just uses its own
  plain name for the staff namespace.
- **Tests:**
  - `locale-parity.test.ts` globs `locales/{en,es}/**/*.json` (recursive),
    so a staff namespace's en/es parity is checked exactly like every other
    namespace's.
  - `app/lazy-routes/staff-locales.test.ts` is the guard, parametrized over
    every namespace it discovers under `locales/en/staff/*.json` (so a new
    one is covered automatically): it asserts
    `i18n.hasResourceBundle('en', ns)` is `false` before importing
    `./staff-locales`, and `true` after, for each one — proving every staff
    namespace truly isn't loaded until the shared registration module runs.
    It then also asserts the REGISTERED bundle deep-equals the actual
    `locales/en/staff/<ns>.json` content on disk (reading the same files
    `staff-locales.ts` itself globs, not a hand-picked spot-check value per
    namespace) — so a new staff namespace needs nothing added to this test
    file either; it's covered automatically by the same glob.
  - A component test that renders a staff-namespace page DIRECTLY (not
    through the lazy route) must import `@/app/lazy-routes/staff-locales`
    for its side effect first, the same way a real lazy route chunk does —
    see `features/educator/pages/educator-students-page.test.tsx`'s top
    import. Skipping this makes `useTranslation('<staff ns>')` try to load
    English from the Spanish-only backend, which now throws loudly rather
    than silently misrendering. Importing this one shared module covers
    EVERY staff namespace at once, so a test whose component depends on
    more than one (e.g. a page that renders both `document-authoring` and
    `educator`) needs only this single import, not one per namespace.
  - `renderInSpanish` needs the staff namespace named explicitly via its
    `ns` option (it's not in `featureNamespaces`, which only lists the
    eager/parent namespaces) — e.g. `renderInSpanish(<Page />, { ns:
    'educator' })`. See the same test file's Spanish test.
  - The lint ratchet (`eslint.config.js`) is untouched by this: converting
    ONE string in `educator-students-page.tsx` does not add
    `src/features/educator/**` to the ratchet — that folder still has
    plenty of unconverted literal strings, and stays un-ratcheted until the
    phase that actually converts it.

**Adding a new staff/admin namespace (conventions for the phases that
follow):**

1. Create `web/src/locales/en/staff/<ns>.json` and
   `web/src/locales/es/staff/<ns>.json` with the same key shape (same rules
   as any namespace — glossary, `{{placeholders}}`, parity test).
2. Add `import type En<Ns> from '@/locales/en/staff/<ns>.json';` to
   `lib/i18n/types.d.ts` and list `<ns>: typeof En<Ns>;` in `EnResources`.
3. Nothing else to wire up for the NAMESPACE itself — `app/lazy-routes/
   staff-locales.ts` discovers every `locales/en/staff/*.json` file
   (including this new one) automatically via its own `import.meta.glob`,
   and all three existing lazy area barrels (`staff-routes.tsx`,
   `district-admin-routes.tsx`, `platform-admin-routes.tsx`) already import
   that one shared module. Skip straight to using the namespace — UNLESS
   the page using it lives behind a brand-new FOURTH lazy area barrel (not
   one of the three above); see the note at the end of "Staff namespaces
   across areas" below for what that barrel itself must do.
4. In the page/component, `useTranslation('<ns>')` (or `useTranslation(['<ns>',
   'common'])` to also reach `common:`) exactly as any other namespace.
5. In that component's OWN tests, import `@/app/lazy-routes/staff-locales`
   (side effect) before rendering; for a Spanish test, also pass `ns: '<ns>'`
   to `renderInSpanish`.
6. Run `npx vitest run src/lib/i18n/locale-parity.test.ts` as usual.
7. If the page itself isn't already a lazy route chunk under one of the
   three area barrels, make it one (`React.lazy` + the barrel re-export +
   a `<Suspense fallback={...}>` around its `<Route element={...}>` — see
   `app/routes.tsx`'s `/educator/*`/`/admin/*` routes for the pattern).

## Adding a key

1. Add the English string to `web/src/locales/en/<namespace>.json` (create the
   namespace file if the feature doesn't have one yet). Never add anything to
   `web/src/lib/i18n/index.ts`'s `resources` for this — `index.ts` discovers
   every `en/*.json` file itself (`import.meta.glob(..., { eager: true })`),
   so a brand-new namespace's English is bundled automatically. **Only add
   `<namespace>` to `index.ts`'s `ns` array if it's `common` or `auth`** — the
   two shell namespaces that must be ready, synchronously, before the app
   chrome renders; every other namespace loads on demand the first time a
   component calls `useTranslation('<namespace>')` (instantly in English,
   already bundled; lazily in Spanish).
   **Do** add one `import type` line for the new namespace to
   `web/src/lib/i18n/types.d.ts`'s `EnResources` — this is the one place that
   still needs hand-maintaining per namespace, so its keys get strict `tsc`
   checking instead of only being caught by the parity test below.
2. Add the Spanish translation at the same key path in
   `web/src/locales/es/<namespace>.json`, following
   [`glossary-es.md`](./glossary-es.md) (neutral Latin American Spanish,
   formal **usted**, sentence case, keep IEP/ETR/IDEA acronyms, keep
   `{{placeholders}}` exactly — en and es must use the same placeholder
   names for a key).
3. Use it with `useTranslation('<namespace>')` and `t('key')` (or
   `t('otherNamespace:key')` to reach another namespace, e.g. `common:ui.cancel`
   — pass an array to `useTranslation`, e.g. `useTranslation(['<namespace>',
   'common'])`, or every OTHER namespace's keys are a `tsc` error from this
   hook's `t`; see the "Typed keys" gotcha above).
   A mixed-language sentence (English glue text around a translated value,
   or vice versa) is a bug even if every individual piece is translated —
   build it as one interpolated key, with `<Trans>` for any bold/styled
   spans, rather than concatenating `t()` calls and raw JSX around a value.
4. Run `npx vitest run src/lib/i18n/locale-parity.test.ts` — it fails the
   build if a key is missing or empty in either language, if Spanish has an
   orphaned key with no English counterpart, or if a key's `{{placeholder}}`
   set differs between en and es (an es-only CLDR plural form like `_many`
   is allowed alongside en's `_one`/`_other`).

A missing Spanish key never shows a blank — i18next falls back to the English
string. The parity test exists so that fallback is never relied upon in
practice.

## The lint ratchet

`eslint.config.js` turns on `i18next/no-literal-string` (mode `jsx-only`, plus
the `aria-label`/`title`/`placeholder`/`label`/`alt` attributes) **only for
folders that have been converted**, so the project-wide ESLint baseline stays
flat while the remaining phases convert the rest of `src`. Converted so far:

- `src/features/auth/**`
- `src/components/layouts/**`
- `src/components/ui/**`
- `src/lib/i18n/**`
- `src/features/staff-invites/pages/staff-accept-invite-page.tsx` and
  `src/features/staff-invites/components/accept-invite-form.tsx`
- `src/features/student/components/student-accept-invite-page.tsx`
- `src/features/children/**`
- `src/features/home/**`
- `src/features/onboarding/**`
- `src/features/notifications/**`
- `src/features/subscription/**`
- `src/features/child-links/**`
- `src/features/sharing/**`
- `src/features/knowledge-base/**`
- `src/features/meetings/components/rsvp-button-group.tsx` (not the rest of
  `features/meetings` — that's phase 3 — but this one component renders
  inside the phase-2 "next meeting"/"upcoming meeting" cards)

`src/features/children/components/child-ieps-tab.tsx` and
`child-etrs-tab.tsx` are explicitly excluded from the `children` ratchet —
they're iep-documents/etr-documents' own pages (a later phase), just hosted
under `children/components/` as the detail page's IEPs/ETRs tabs.

Test files (`*.test.ts`/`*.test.tsx`) are excluded everywhere — they stay in
English by design (see `test/setup.ts`); a test that specifically needs
Spanish uses the `renderInSpanish` helper in `src/test/i18n-test-utils.tsx`
instead of switching the lint rule's scope.

The rule becomes global (non-test `web/src`) in the plan's last phase.

## Namespace coverage and review status

Spanish throughout is **drafted by Claude**, following the glossary. Mark a
namespace "Reviewed" here only after a native Spanish speaker (ideally with
special-education familiarity) has signed off — see
[`glossary-es.md`](./glossary-es.md#review-status). Reviewing is a
prerequisite for marketing Spanish, not for shipping it.

| Namespace | Converted in | Spanish status |
|---|---|---|
| `common` | Phase 1 | Draft — needs native review |
| `auth` | Phase 1 | Draft — needs native review |
| `children` | Phase 2 | Draft — needs native review |
| `home` | Phase 2 | Draft — needs native review |
| `onboarding` | Phase 2 | Draft — needs native review |
| `notifications` | Phase 2 | Draft — needs native review |
| `subscription` | Phase 2 | Draft — needs native review |
| `child-links` | Phase 2 | Draft — needs native review |
| `sharing` | Phase 2 | Draft — needs native review |
| `knowledge-base` | Phase 2 (page chrome only — article bodies stay English) | Draft — needs native review |
| `iep-documents` | Phase 3 | Draft — needs native review |
| `etr-documents` | Phase 3 | Draft — needs native review |
| `analysis` | Phase 3 | Draft — needs native review |
| `iep-comparison` | Phase 3 | Draft — needs native review |
| `iep-versions` | Phase 3 | Draft — needs native review |
| `progress-reports` | Phase 3 | Draft — needs native review |
| `goals` | Phase 3 | Draft — needs native review |
| `advocacy-goals` | Phase 3 | Draft — needs native review |
| `meeting-prep` | Phase 3 | Draft — needs native review |
| `journal` | Phase 3 | Draft — needs native review |
| `advocate` | Phase 3 | Draft — needs native review |
| `shared-drafts` | Phase 3 | Draft — needs native review |
| `draft-sharing` | Phase 3 | Draft — needs native review |
| `meetings` | Phase 3 | Draft — needs native review |
| `student` | Phase 3 | Draft — needs native review |
| `educator (staff)` | Phase 5 | Draft — needs native review |
| `calendar` | Phase 5 | Draft — needs native review |
| `meeting-brief (staff)` | Phase 5 | Draft — needs native review |
| `document-authoring (staff)` | Phase 5 | Draft — needs native review |
| `document-authoring-shared` | Phase 5 | Draft — needs native review |
| `evaluation (staff)` | Phase 5 | Draft — needs native review |
| `obligations (staff)` | Phase 5 | Draft — needs native review |
| `family-contact (staff)` | Phase 5 | Draft — needs native review |
| `meetings-staff (staff)` | Phase 5 | Draft — needs native review |
| `contributions` | Phase 5 | Draft — needs native review |
| `educator` (staff — `locales/{en,es}/staff/educator.json`, lazy-route-registered English; see "Staff and admin namespaces" above) | Phase 5 (foundation example — one string; the rest of `features/educator` converts later) | Draft — needs native review |

## Tests

- **Key parity:** `src/lib/i18n/locale-parity.test.ts` — every `en` key exists
  in `es` and is non-empty (and vice versa, so nothing is orphaned), every
  key's `{{placeholder}}` set matches between `en`/`es`, and every key's set
  of `<tag>`/`</tag>` names (the components a `<Trans>` call substitutes)
  matches too — a tag renamed on one side and not the other (e.g. the
  reserved-word fix that turned `<context>` into `<inviter>`) fails this
  immediately instead of silently breaking Spanish's styling.
- **Resolution order:** `src/lib/i18n/detect.test.ts`.
- **Key typing canary:** `src/lib/i18n/key-typing.test.tsx` — `tsc`-only
  (`npm run test:types`); a handful of `// @ts-expect-error` lines on known-
  unknown keys (`i18n.t`, a namespaced `useTranslation().t` — including
  `children`, a feature namespace, since phase 2 review made every namespace
  strict — and `Trans`'s `i18nKey`) so a future change that loosens the
  generated key typing to `any` fails the build via "unused
  `@ts-expect-error`" instead of quietly disabling typo-checking everywhere.
  `.tsx`, not `.ts`: the `Trans` canary needs REAL JSX —
  `React.createElement(Trans, {...})` doesn't get the same per-element
  generic inference JSX syntax does.
- **Formatting:** `src/lib/format-date.test.ts` (and any other
  locale-sensitive formatter gets its own `en`/`es` cases as it's converted).
- **Spanish render tests:** one per converted page/component, asserting the
  heading and primary controls are Spanish by role/name. Use `renderInSpanish`
  from `src/test/i18n-test-utils.tsx`:

  ```tsx
  import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';

  afterEach(() => resetTestLanguage());

  it('renders in Spanish', async () => {
    await renderInSpanish(<MyPage />);
    expect(screen.getByRole('heading', { name: 'Texto esperado' })).toBeInTheDocument();
  });
  ```

  Every other test keeps using plain `render` and English text queries — the
  global test setup initializes i18next with English synchronously so none of
  the ~600 existing English queries needed to change. **Pass `ns` to
  `renderInSpanish`** only if a namespace exists outside `locales/es/*.json`
  (it never does in practice, but the option stays) — it preloads every
  namespace (`featureNamespaces`, from `lib/i18n/index.ts` — every namespace
  now, not just feature-level ones, since Spanish is lazy for `common`/`auth`
  too) in Spanish before `render`, same reasoning as `test/setup.ts` below.
- **No raw key leaking through, for every test, automatically:**
  `test/setup.ts` turns on `saveMissing` with a `missingKeyHandler` that
  throws on any genuinely missing translation key — one resolved without a
  *meaningful* `defaultValue` (`opt.defaultValue !== key`) **and** whose
  namespace has actually finished loading for that language
  (`i18n.hasLoadedNamespace`). English is always already loaded (every
  `en/*.json` file is bundled eagerly — see "Resources" above), so that
  second check only ever matters for Spanish: a namespace's Spanish data
  loads lazily, so the very first render of a component calling
  `useTranslation('<namespace>')` in a worker that hasn't loaded it yet in
  Spanish would otherwise look exactly like a missing key. `test/setup.ts`
  preloads every namespace in `featureNamespaces` in English up front (now
  effectively a confirmation, not a real load, since English needs no
  fetch); `renderInSpanish` does the equivalent, real, load for Spanish. The
  `hasLoadedNamespace` guard in `missingKeyHandler` is the backstop for
  anything that still races the Spanish load. `orgRoleLabel`'s
  unrecognized-role fallback
  (`{ defaultValue: name }`) is exempted the same way as a real "has a
  default" case; a bare `<Trans i18nKey="some.key" />` with no children/
  `defaults`/`tOptions.defaultValue` of its own is NOT — react-i18next
  quietly sets `opt.defaultValue` to the key itself in that case, which is
  indistinguishable from "no default" and must still throw on a typo. A
  Spanish render test no longer needs its own
  `expect(document.body.textContent).not.toMatch(/ns:[a-zA-Z.]+/)` — a
  missing key fails the test the moment `t()` is called, for every
  namespace, not just the one a hand-written regex happened to name.
- **`<html lang>` follows the active language:** `src/lib/i18n/index.test.ts`
  asserts `document.documentElement.lang` updates on `changeLanguage`.

## Staff namespaces across areas

`app/lazy-routes/staff-locales.ts` registers every staff/admin English namespace it finds under `locales/en/staff/*.json` (one eager `import.meta.glob`, not a hand-written import per namespace). All three lazy area chunks (staff, district-admin, platform-admin) import this one shared module, because shared staff components render across areas. A new staff namespace needs nothing added here or to an individual barrel — dropping its `en/staff/<ns>.json` file on disk is enough; the glob picks it up. Tests that render staff pages import this module directly.

**Any new lazy area barrel must import `./staff-locales` too.** This is per-BARREL, not per-namespace: `staff-locales.ts` registering a namespace's English does nothing for a page unless something on that page's own lazy-chunk path imported `staff-locales.ts` for its side effect before the page renders — which is exactly what `staff-routes.tsx`/`district-admin-routes.tsx`/`platform-admin-routes.tsx` each do today. If a future phase adds a fourth lazy area (a new top-level `React.lazy` barrel under `app/lazy-routes/`, grouping some new set of routes the way those three group theirs) and any page in it uses a staff namespace, that new barrel file needs its own `import '@/app/lazy-routes/staff-locales';` at the top — otherwise every staff namespace it uses throws the `[i18n] unexpected backend request for en/<ns>` error the first time one of its pages renders (see "How English actually loads, then" above), since nothing on that barrel's own import path would have registered it. This is a one-line addition when it's needed; it needs no new line for a namespace added to an EXISTING barrel's area, which is the common case covered by the numbered steps above.

## Server-side AI language (Phase 3)

- Non-parser prompt builders append `ResponseLanguage.SystemLine(culture)`. It's empty for English, and for Spanish it's a fixed instruction carrying the glossary terms. The IEP/ETR document parsers never get it, and a test enforces that.
- Persisted AI artifacts store `Language` and expose `generatedLanguage`: analysis runs, meeting prep, advocate messages, family meeting summaries, draft explanations and answers, and progress-report analyses.
- The web renders `GeneratedLanguageNotice` when that language differs from the viewer's. Nothing is regenerated automatically.
- Canned AI strings live in `Ai.resx`.
