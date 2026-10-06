# Multilingual site (i18n)

English and Spanish ship together. This page explains how to add a translation
key, how the lint ratchet works, how the parity test is enforced, and which
namespaces are converted so far. For Spanish wording rules (tone, formality,
special-education terminology), see [`glossary-es.md`](./glossary-es.md).

## How it's built

- **Library:** `i18next` + `react-i18next`, with `i18next-browser-languagedetector`
  for pre-login detection and `i18next-resources-to-backend` for lazy-loading
  Spanish.
- **Resources:** `web/src/locales/{en,es}/<namespace>.json`. English is bundled
  (static imports in `web/src/lib/i18n/index.ts`); Spanish is lazy-loaded per
  namespace via `import.meta.glob`.
- **Namespaces:** one per feature folder, plus `common` for shared chrome
  (layouts, `components/ui/*`, enum-label helpers, generic errors). A page
  reads from its own feature namespace and `common`.
- **Typed keys:** `web/src/lib/i18n/types.d.ts` derives `CustomTypeOptions`
  from the English resources, so `t('ns:unknownKey')` is a `tsc` error, not a
  runtime surprise.
- **Language resolution** (`web/src/lib/i18n/detect.ts`), in order:
  1. the signed-in user's saved `preferredLanguage` (applied by `AuthProvider`
     after `/me`, and on every login/session-restore);
  2. a choice made before signing in (`localStorage`, or a `?lang=en|es` query
     param honored once by `useLanguageQueryParam`);
  3. the browser's `navigator.languages` (`es-*` → `es`);
  4. `en`.
- **Switching:** `web/src/lib/i18n/language-switcher.tsx` — the one
  `LanguageSwitcher` component, in the auth layout footer, the sidebar footer,
  and the Profile page's Language field. Switching updates `<html lang>`
  immediately and persists the choice (account PUT when signed in,
  `localStorage` otherwise).
- **Formatting:** `web/src/lib/i18n/format.ts` (`getActiveLanguage`,
  `formatDate`, `formatNumber`) plus `lib/format-date.ts` and
  `lib/relative-time.ts`, all keyed on the active i18next language rather
  than a hard-coded locale.

## Adding a key

1. Add the English string to `web/src/locales/en/<namespace>.json` (create the
   namespace file if the feature doesn't have one yet — also add it to the
   `ns` array in `web/src/lib/i18n/index.ts` and to `resources` there if it
   should be bundled rather than lazy).
2. Add the Spanish translation at the same key path in
   `web/src/locales/es/<namespace>.json`, following
   [`glossary-es.md`](./glossary-es.md) (neutral Latin American Spanish,
   formal **usted**, sentence case, keep IEP/ETR/IDEA acronyms, keep
   `{{placeholders}}` exactly).
3. Use it with `useTranslation('<namespace>')` and `t('key')` (or
   `t('otherNamespace:key')` to reach another namespace, e.g. `common:ui.cancel`).
4. Run `npx vitest run src/lib/i18n/locale-parity.test.ts` — it fails the
   build if a key is missing or empty in either language, or if Spanish has an
   orphaned key with no English counterpart.

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
  heading and primary controls are Spanish by role/name and that no raw
  `namespace:key` string is visible. Use `renderInSpanish` from
  `src/test/i18n-test-utils.tsx`:

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
