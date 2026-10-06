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
  (static imports in `web/src/lib/i18n/index.ts`); Spanish is lazy-loaded per
  namespace via `import.meta.glob`.
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
  pages that never render a component from that feature.
- **Typed keys:** `web/src/lib/i18n/types.d.ts` derives `CustomTypeOptions`
  from the `resources` object `index.ts` exports, so `t('ns:unknownKey')` is a
  `tsc` error, not a runtime surprise, and registering a new *bundled*
  namespace is one import + one entry in that single `resources` object
  (lazy, feature-level namespaces aren't part of `resources` and don't need
  a `types.d.ts` change — their keys are still typed once their English JSON
  module is imported somewhere, which `useTranslation` itself doesn't
  require, so a purely-lazy namespace's keys are checked by the parity test
  below rather than by `tsc`).
- **Language resolution** (`web/src/lib/i18n/detect.ts`), in order:
  1. the signed-in session's **cached** account language — the
     `preferredLanguage` on the last `/me` response `lib/auth` stored in
     `localStorage`, read synchronously so a page *reload* doesn't flash the
     pre-login/browser language first;
  2. a choice made before signing in, or on a prior visit after signing out
     (`localStorage`, or a `?lang=en|es` query param honored once by
     `useLanguageQueryParam`);
  3. the browser's `navigator.languages` (`es-*` → `es`);
  4. `en`.

  A signed-in user's language is *confirmed* separately, once `/me`
  resolves: `AuthProvider.loadUser`/`refreshUser` call `syncLanguagePreference`
  with the generation captured before that fetch started, so a stale response
  (one that resolves after a newer explicit switch) can never revert the
  language or persist a now-outdated choice — see the doc comments on
  `AuthProvider.setLanguage`/`syncLanguagePreference` for the full race.
- **Switching:** `web/src/lib/i18n/language-switcher.tsx` — the one
  `LanguageSwitcher` component, in the auth layout footer, the sidebar footer,
  and the Profile page's Language field (which also passes a `hint`).
  Switching updates `<html lang>` immediately and persists the choice
  (account PUT when signed in, `localStorage` otherwise). Signing out keeps
  the active language by writing it to the pre-login key, rather than
  clearing it, so the login page doesn't fall back to the browser language.
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

## Tests

- **Key parity:** `src/lib/i18n/locale-parity.test.ts` — every `en` key exists
  in `es` and is non-empty (and vice versa, so nothing is orphaned).
- **Resolution order:** `src/lib/i18n/detect.test.ts`.
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
  the ~600 existing English queries needed to change.
- **No raw key leaking through, for every test, automatically:**
  `test/setup.ts` turns on `saveMissing` with a `missingKeyHandler` that
  throws on any genuinely missing translation key (one resolved without an
  explicit `defaultValue`, e.g. `orgRoleLabel`'s unrecognized-role fallback,
  which is exempted). A Spanish render test no longer needs its own
  `expect(document.body.textContent).not.toMatch(/ns:[a-zA-Z.]+/)` — a
  missing key fails the test the moment `t()` is called, for every
  namespace, not just the one a hand-written regex happened to name.
- **`<html lang>` follows the active language:** `src/lib/i18n/index.test.ts`
  asserts `document.documentElement.lang` updates on `changeLanguage`.
