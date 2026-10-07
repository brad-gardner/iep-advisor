import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import resourcesToBackend from 'i18next-resources-to-backend';
import * as Sentry from '@sentry/react';
import enCommon from '@/locales/en/common.json';
import enAuth from '@/locales/en/auth.json';
import { detectInitialLanguage, SUPPORTED_LANGUAGES, DEFAULT_LANGUAGE } from './detect';

export const defaultNS = 'common';

// The two SHELL namespaces (`common`, `auth`) are bundled for English (no
// network round-trip, no flash of missing text on the very first paint) —
// see `ns` below. Spanish always loads lazily, per namespace, on demand, via
// the resourcesToBackend plugin below. So does English for every OTHER,
// feature-level namespace (`children`, `home`, …): those aren't bundled in
// either language, so the very first `useTranslation('<namespace>')` call
// for one — in English or Spanish — goes through the same backend.
export const resources = {
  en: { common: enCommon, auth: enAuth },
} as const;

// Every locale JSON file, for both languages, eagerly discovered but lazily
// IMPORTED (import.meta.glob without `eager: true` yields loader functions,
// not the modules themselves) — Vite code-splits each into its own chunk.
// `partialBundledLanguages: true` below means this is only ever actually
// consulted for a (language, namespace) pair NOT already in `resources`
// above — i.e. `en/common` and `en/auth` are served directly from the
// bundle, never through this backend, even though their files also match
// this glob.
const localeLoaders = import.meta.glob(['/src/locales/en/*.json', '/src/locales/es/*.json']) as Record<
  string,
  () => Promise<{ default: Record<string, unknown> }>
>;

// Every feature-level namespace name, derived from the `en/*.json` files on
// disk (minus the two shell namespaces, already bundled above) — so this
// list updates itself as phases add namespaces, with nothing to hand-
// maintain here. Exported for `test/setup.ts`: tests stay English by
// default and mostly assert synchronously right after `render()`, so it
// preloads every one of these before any test runs, the same way the shell
// namespaces are already synchronously available via `resources`. Without
// that, the FIRST test in a worker to render a component that calls
// `useTranslation('<feature namespace>')` would see the raw `ns:key` text
// for one tick (react-i18next re-renders once the lazy load resolves, but a
// synchronous `getByText`/`getByRole` right after `render()` runs before
// that).
export const featureNamespaces = Object.keys(localeLoaders)
  .filter((path) => path.startsWith('/src/locales/en/'))
  .map((path) => path.slice('/src/locales/en/'.length, -'.json'.length))
  .filter((ns) => ns !== 'common' && ns !== 'auth');

// Exported so callers that need to know initialization has settled (the
// test setup, chiefly — see `test/setup.ts`) can await it instead of relying
// on timing. Nothing else in app code should need this: `useTranslation`'s
// own re-render-on-ready behavior (via `react: { useSuspense: false }`
// below) handles the UI case.
export const i18nReady = i18next
  .use(
    resourcesToBackend(async (language: string, namespace: string) => {
      const path = `/src/locales/${language}/${namespace}.json`;
      const loader = localeLoaders[path];
      if (!loader) {
        throw new Error(`[i18n] no locale file for ${language}/${namespace}`);
      }
      const mod = await loader();
      return mod.default;
    })
  )
  .use(initReactI18next)
  .init({
    resources,
    // The shell namespaces above are used directly; the backend is only
    // consulted for a (language, namespace) pair not already bundled there
    // — every namespace in Spanish, and every feature-level namespace (not
    // `common`/`auth`) in English too.
    partialBundledLanguages: true,
    // Resolved once, synchronously, by `detectInitialLanguage` (cached
    // account language, else a pre-login choice, else the browser, else
    // `en` — see `detect.ts`). No detector plugin: `AuthProvider` is what
    // applies a signed-in user's *confirmed* preference once `/me`
    // resolves, and `useLanguageQueryParam` applies a `?lang=` visit: both
    // call `i18n.changeLanguage` directly rather than through a detector.
    lng: detectInitialLanguage(),
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES,
    load: 'languageOnly',
    ns: ['common', 'auth'],
    defaultNS,
    interpolation: { escapeValue: false },
    returnNull: false,
    // No Suspense boundary wraps most of the app (only the lazy Advocate
    // route chunk has one), so a component that calls `t()` before the
    // active language's namespace finishes loading (e.g. the very first
    // render in Spanish) must render synchronously with a fallback instead
    // of suspending. react-i18next still re-renders it once the namespace
    // resolves (via its own i18next event subscription).
    react: { useSuspense: false },
  });

// Keep the document's declared language in sync with i18next's resolved
// language on every change (including the very first resolution).
// `resolvedLanguage` (not the `lng` the event carries) is what actually
// loaded — they can differ for an unsupported/regional tag.
i18next.on('languageChanged', () => {
  if (typeof document !== 'undefined') {
    document.documentElement.lang = i18next.resolvedLanguage ?? DEFAULT_LANGUAGE;
  }
});

// A namespace fails to load (e.g. the network drops mid-way through
// fetching the lazy Spanish chunk): never leave the UI showing raw
// `ns:key` strings indefinitely. Log it and fall back to English, which is
// always bundled and therefore always available.
i18next.on('failedLoading', (lng, ns, msg) => {
  Sentry.captureException(new Error(`[i18n] failed loading ${lng}/${ns}: ${msg}`));
  if (lng !== DEFAULT_LANGUAGE) {
    void i18next.changeLanguage(DEFAULT_LANGUAGE);
  }
});

export default i18next;
