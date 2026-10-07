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
- **Resources:** `web/src/locales/{en,es}/<namespace>.json`. English is bundled
  (static imports in `web/src/lib/i18n/index.ts`) **only for the two shell
  namespaces** (`common`, `auth`). Every other, feature-level namespace loads
  lazily **in both languages** via the same `resourcesToBackend`
  plugin/`import.meta.glob` (`index.ts`'s `localeLoaders`, now globbing
  `locales/en/*.json` and `locales/es/*.json`) — `partialBundledLanguages:
  true` means the backend is only ever actually consulted for a
  (language, namespace) pair not already in the bundle, so `en/common` and
  `en/auth` are served from the bundle while, say, `en/children` and
  `es/children` both go through the backend. Phase 1 only needed Spanish
  lazy-loaded because every namespace that existed then (`common`, `auth`)
  was already bundled in English; from the first feature-level namespace
  (phase 2's `children`) onward, English needs the same lazy path Spanish
  always had.
- **Namespaces:** one per feature folder, plus `common` for shared chrome
  (layouts, `components/ui/*`, enum-label helpers, generic errors). A page
  reads from its own feature namespace and `common`. **`index.ts`'s `ns`
  option only ever lists the shell namespaces (`common`, `auth`)** — those
  two load eagerly because the shell (layouts, the switcher, auth pages)
  needs them before any route-level code splitting kicks in. Every other,
  feature-level namespace is **not** added there; it loads on demand the
  first time a component calls `useTranslation('<that namespace>')`
  (react-i18next's `useTranslation` triggers the load itself and re-renders
  once it resolves — see `react: { useSuspense: false }` in `index.ts`).
  Adding a namespace to `ns` would make every page pay for loading it, even
  pages that never render a component from that feature. Because this applies
  in English too now, a component's very first render (in either language)
  can momentarily see an unloaded namespace — harmless in practice (the
  re-render lands before anyone notices, and `test/setup.ts` /
  `renderInSpanish` preload every known feature namespace for tests — see
  below), but it is why `missingKeyHandler` specifically exempts
  "not loaded yet" from "missing" (see Tests).
- **Typed keys:** `web/src/lib/i18n/types.d.ts` derives `CustomTypeOptions`
  from the `resources` object `index.ts` exports, so `t('common:unknownKey')`/
  `t('auth:unknownKey')` is a `tsc` error, not a runtime surprise, and
  registering a new *bundled* namespace is one import + one entry in that
  single `resources` object. A lazy, feature-level namespace (`children`,
  `home`, `onboarding`, `notifications`, `subscription`, `child-links`,
  `sharing`, `knowledge-base`, …) isn't part of `resources` and can't be
  derived the same way — react-i18next's generated `useTranslation`/`t`
  overloads still need SOME entry for it in `CustomTypeOptions.resources`,
  strictly-typed or not, or `useTranslation('<namespace>')` itself fails to
  compile. Each one gets a line in `types.d.ts`'s `LazyNamespaces` with a
  loose `Record<string, string>` shape: `useTranslation('children')` and
  `t('children:anyKey')` compile, but an individual KEY typo in a lazy
  namespace is caught at runtime by the parity test below, not by `tsc`. Add
  one `LazyNamespaces` line per new feature-level namespace.
  **Gotcha:** don't build a lazy namespace's key dynamically (a template
  literal or a variable) in the SAME call that also passes `options` (e.g.
  `{ defaultValue }`) — `i18n.t(\`sharing:role.${role}\`, { defaultValue })`
  fails to type-check against a loose `Record<string, string>` namespace (a
  `keyof Record<string, string>` quirk feeding a nonsensical `string`-method
  union into the error). A `switch` over literal keys (see
  `features/sharing/lib/role-label.ts`, `features/knowledge-base/components/
  category-tabs.tsx`), or a dynamic key with NO extra options, both compile
  fine.
- **Language resolution** (`web/src/lib/i18n/detect.ts`), for what a page
  *shows*, in order:
  1. the signed-in session's **cached** account language — the
     `preferredLanguage` on the last `/me` response `lib/auth` stored in
     `localStorage`, read synchronously so a page *reload* doesn't flash the
     pre-login/browser language first;
  2. the visitor's **explicit** pre-login choice — the switcher, or a
     `?lang=en|es` query param honored once by `useLanguageQueryParam`,
     while signed out (`getPreLoginLanguage`);
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
  choice is guaranteed to be the last write the server sees.
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
  everywhere `ChildProfile.gradeLevel`/`disabilityCategory` are displayed.
  `features/sharing/lib/role-label.ts` (`sharing:role.*`) covers the
  child-access share role (`owner`/`viewer`/`collaborator`) — written as a
  `switch` over literal keys rather than a dynamic one (see the
  `LazyNamespaces` gotcha above, since `sharing` is a lazy namespace).
- **DB-authored content stays English, with a note**: `features/knowledge-base`
  converts page chrome (search, category tabs, the disclaimer) but not an
  article's own `title`/`content`/`tags`/`state` (district/DB content, like
  `orgRoleLabel`'s "stored value" — translating it is the plan's own
  knowledge-base-articles follow-up, not this phase). Each entry card shows a
  small `knowledge-base:entryCard.availableInEnglish` note when
  `i18n.resolvedLanguage === 'es'`, so a Spanish reader is told plainly
  rather than left to assume the article itself was translated.

## Adding a key

1. Add the English string to `web/src/locales/en/<namespace>.json` (create the
   namespace file if the feature doesn't have one yet). **Only add it to the
   `ns` array and `resources` object in `web/src/lib/i18n/index.ts` if
   `<namespace>` is `common` or `auth`** — the two shell namespaces that must
   be ready before the app chrome renders. Any other, feature-level
   namespace stays out of `ns`/`resources`: it's picked up automatically the
   first time a component calls `useTranslation('<namespace>')`, and listing
   it in `ns` would make it load eagerly for every page instead of only the
   pages that use it.
2. Add the Spanish translation at the same key path in
   `web/src/locales/es/<namespace>.json`, following
   [`glossary-es.md`](./glossary-es.md) (neutral Latin American Spanish,
   formal **usted**, sentence case, keep IEP/ETR/IDEA acronyms, keep
   `{{placeholders}}` exactly — en and es must use the same placeholder
   names for a key).
3. Use it with `useTranslation('<namespace>')` and `t('key')` (or
   `t('otherNamespace:key')` to reach another namespace, e.g. `common:ui.cancel`).
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
  unknown keys (`i18n.t`, a namespaced `useTranslation().t`, `Trans`'s
  `i18nKey`) so a future change that loosens the generated key typing to
  `any` fails the build via "unused `@ts-expect-error`" instead of quietly
  disabling typo-checking everywhere. `.tsx`, not `.ts` (phase 2): the
  `Trans` canary needs REAL JSX — `React.createElement(Trans, {...})` lost
  its per-element generic inference once a lazy, loosely-typed namespace
  existed (see the `LazyNamespaces` gotcha above), so the bare-`createElement`
  version of this check went vacuously green. The file also has a
  non-`@ts-expect-error` line asserting a lazy namespace's arbitrary key
  (`useTranslation('children')` + `t('anything')`) DOES compile — a future
  tightening of `LazyNamespaces` that breaks this is caught as an ordinary
  `tsc` error, not silently.
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
  (it never does in practice, but the option stays) — it preloads every known
  feature namespace (`featureNamespaces`, from `lib/i18n/index.ts`) in
  Spanish before `render`, same reasoning as `test/setup.ts` below.
- **No raw key leaking through, for every test, automatically:**
  `test/setup.ts` turns on `saveMissing` with a `missingKeyHandler` that
  throws on any genuinely missing translation key — one resolved without a
  *meaningful* `defaultValue` (`opt.defaultValue !== key`) **and** whose
  namespace has actually finished loading for that language
  (`i18n.hasLoadedNamespace`). That second check matters as of phase 2:
  every feature-level namespace loads lazily in BOTH languages now (see
  "Resources" above), so the very first render of a component calling
  `useTranslation('<namespace>')` in a worker that hasn't loaded it yet would
  otherwise look exactly like a missing key. `test/setup.ts` sidesteps this
  for English by preloading every namespace in `featureNamespaces` up front,
  before any test runs; `renderInSpanish` does the same for Spanish. The
  `hasLoadedNamespace` guard in `missingKeyHandler` is the backstop for
  anything that still races it. `orgRoleLabel`'s unrecognized-role fallback
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
