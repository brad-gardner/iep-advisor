import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import resourcesToBackend from 'i18next-resources-to-backend';
import enCommon from '@/locales/en/common.json';
import enAuth from '@/locales/en/auth.json';
import { preLoginDetector, SUPPORTED_LANGUAGES, DEFAULT_LANGUAGE } from './detect';

export const defaultNS = 'common';

// English is bundled (no network round-trip, no flash of missing text on the
// very first paint). Spanish is lazy — loaded per namespace, on demand, by
// the resourcesToBackend plugin below.
export const resources = {
  en: { common: enCommon, auth: enAuth },
} as const;

// Every Spanish namespace file, eagerly discovered but lazily IMPORTED
// (import.meta.glob without `eager: true` yields loader functions, not the
// modules themselves) — Vite code-splits each into its own chunk.
const esLoaders = import.meta.glob('/src/locales/es/*.json') as Record<
  string,
  () => Promise<{ default: Record<string, unknown> }>
>;

const languageDetector = new LanguageDetector();
languageDetector.addDetector(preLoginDetector);

// Exported so callers that need to know initialization has settled (the
// test setup, chiefly — see `test/setup.ts`) can await it instead of relying
// on timing. Nothing else in app code should need this: `useTranslation`'s
// own re-render-on-ready behavior (via `react: { useSuspense: false }`
// below) handles the UI case.
export const i18nReady = i18next
  .use(languageDetector)
  .use(
    resourcesToBackend(async (language: string, namespace: string) => {
      const path = `/src/locales/${language}/${namespace}.json`;
      const loader = esLoaders[path];
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
    // English namespaces above are used directly; the backend is only
    // consulted for languages/namespaces not already bundled (i.e. es/*).
    partialBundledLanguages: true,
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES,
    load: 'languageOnly',
    ns: ['common', 'auth'],
    defaultNS,
    detection: {
      order: ['iep-prelogin'],
      caches: [],
    },
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
i18next.on('languageChanged', (lng) => {
  if (typeof document !== 'undefined') {
    document.documentElement.lang = lng;
  }
});

export default i18next;
