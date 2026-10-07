import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import resourcesToBackend from 'i18next-resources-to-backend';
import * as Sentry from '@sentry/react';
import { detectInitialLanguage, SUPPORTED_LANGUAGES, DEFAULT_LANGUAGE } from './detect';
import type { EnResources } from './types';

export const defaultNS = 'common';

// Every English namespace is bundled eagerly, with no network round-trip and
// no flash of raw `ns:key` text on first paint — and, just as importantly, no
// way to get permanently STUCK on raw keys if a lazy chunk fails to load over
// a flaky connection, since English never depends on one loading at all (see
// the plan's "Decisions added during implementation": lazy English namespaces
// showed raw keys before load and got stuck on them when a chunk failed).
// `import.meta.glob(..., { eager: true })` discovers every `en/*.json` file
// on disk and inlines its content directly into this module — a new
// namespace file added in a later phase needs no corresponding line here.
const enModules = import.meta.glob('/src/locales/en/*.json', { eager: true }) as Record<
  string,
  { default: Record<string, unknown> }
>;

function namespaceOf(path: string, root: string): string {
  return path.slice(root.length, -'.json'.length);
}

/** The namespace a locale file stands for: its filename, minus `.json`, regardless of subdirectory (`locales/es/staff/educator.json` → `educator`, same as `locales/en/educator.json` would). */
function namespaceOfBasename(path: string): string {
  return path.slice(path.lastIndexOf('/') + 1, -'.json'.length);
}

// The runtime value, discovered automatically by the glob above — adding a
// namespace file is enough; nothing here needs updating. Its literal,
// per-namespace TYPE can't come from a dynamic glob (Vite's `import.meta.glob`
// types every match as the same generic module shape, not a literal path-to-
// shape map — see `types.d.ts`), so `types.d.ts` derives `CustomTypeOptions`
// from a small, parallel set of `import type` lines instead — those cost
// nothing at runtime (erased by `tsc`) and are the one thing that still needs
// a line added per new namespace.
export const resources = {
  en: Object.fromEntries(
    Object.entries(enModules).map(([path, mod]) => [namespaceOf(path, '/src/locales/en/'), mod.default])
  ),
} as const;

// Every namespace name, derived from the `en/*.json` files on disk — so this
// list updates itself as phases add namespaces, with nothing to hand-
// maintain here. Exported for `test/setup.ts` (asserting every namespace
// is bundled) and `test/i18n-test-utils.tsx`'s `renderInSpanish` (preloading
// every namespace in Spanish before render — Spanish is still lazy; see
// `esLoaders` below).
export const featureNamespaces = Object.keys(resources.en);

// Spanish, discovered eagerly but imported LAZILY (import.meta.glob without
// `eager: true` yields loader functions, not the modules themselves) — Vite
// code-splits each namespace into its own chunk, fetched only once a
// Spanish-reading visitor actually needs it. English never goes through this
// backend at all now; every parent/shell `en/*.json` file is already in
// `resources` above, and a staff/admin namespace's English arrives instead
// via `registerEnglishNamespace` (below), called by that namespace's own
// route chunk.
//
// `**/*.json` (recursive, unlike `enModules`'s single-level `*.json` above)
// so this one loader map also covers `locales/es/staff/<ns>.json` — a
// staff/admin namespace's Spanish stays lazy exactly like every other
// namespace's; only its ENGLISH loading path differs (see
// `registerEnglishNamespace`). Keyed by NAMESPACE (the filename, regardless
// of which subdirectory it lives under) rather than by full path, so a
// staff namespace's directory (`staff/`) is irrelevant to resolving it —
// the namespace name is what every `useTranslation(ns)` call and
// `resourcesToBackend` request actually use. This means a staff namespace's
// name must not collide with a parent/shell one (`docs/i18n/README.md`).
const esModules = import.meta.glob('/src/locales/es/**/*.json') as Record<
  string,
  () => Promise<{ default: Record<string, unknown> }>
>;
const esLoaders = new Map(Object.entries(esModules).map(([path, loader]) => [namespaceOfBasename(path), loader]));

// Exported so callers that need to know initialization has settled (the
// test setup, chiefly — see `test/setup.ts`) can await it instead of relying
// on timing. Nothing else in app code should need this: `useTranslation`'s
// own re-render-on-ready behavior (via `react: { useSuspense: false }`
// below) handles the UI case.
export const i18nReady = i18next
  .use(
    resourcesToBackend(async (language: string, namespace: string) => {
      // English never reaches this backend — a parent/shell namespace is
      // always already in `resources` above, and a staff/admin namespace's
      // English is registered synchronously by its own route chunk (see
      // `registerEnglishNamespace`) before any component can call
      // `useTranslation` for it. Reaching here for `en` means that
      // invariant broke (a component rendered before its chunk's
      // `staff-locales`-style registration ran) — throw loudly rather than
      // silently serving the WRONG language's file for the namespace.
      if (language !== 'es') {
        throw new Error(
          `[i18n] unexpected backend request for ${language}/${namespace} — English must be bundled eagerly or registered via registerEnglishNamespace, never fetched`
        );
      }
      const loader = esLoaders.get(namespace);
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
    // English is fully bundled in `resources` above; the backend is only
    // ever actually consulted for Spanish, the one language NOT present
    // there.
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
    // Only the two shell namespaces are EAGERLY loaded at init (so the
    // layout, sidebar, switcher, and auth pages never wait on anything
    // before first paint). Every other namespace's ENGLISH data already
    // sits in `resources` above — `useTranslation('<namespace>')` sees it as
    // already loaded with no network call, for any namespace, the instant a
    // component first asks for it. Its SPANISH data is not preseeded, so the
    // same first call lazily fetches just that one namespace's `es` chunk
    // via `esLoaders` above. Listing every namespace here instead would
    // force Spanish to eagerly fetch every chunk up front, defeating the
    // whole point of lazy-loading Spanish per namespace.
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

// A namespace fails to load: since Spanish is the only language that ever
// loads through the network backend above (English is always already in
// `resources`), this can only mean a Spanish chunk failed mid-fetch (e.g. the
// network drops). Never leave the UI showing raw `ns:key` strings
// indefinitely — log it and fall back to English, which needs no load at all
// and is therefore always available immediately.
i18next.on('failedLoading', (lng, ns, msg) => {
  Sentry.captureException(new Error(`[i18n] failed loading ${lng}/${ns}: ${msg}`));
  if (lng !== DEFAULT_LANGUAGE) {
    void i18next.changeLanguage(DEFAULT_LANGUAGE);
  }
});

// A staff/admin namespace's English (`locales/en/staff/<ns>.json`) is
// deliberately NOT part of `enModules`/`resources` above — that glob only
// matches direct children of `locales/en/`, not `locales/en/staff/*` — so it
// never enters the main chunk. Instead, `app/lazy-routes/staff-locales.ts`
// (one shared module, imported by all three lazy area route barrels) globs
// every `en/staff/<ns>.json` file (so THAT import, not this one, is what
// makes Vite split them into those already-lazy chunks) and calls this once
// per file, at module top level, before any of those chunks' page
// components can render. `deep: true, overwrite: true` matches a normal
// `addResourceBundle` full-replace of the namespace — there is never a
// partial/merge case here, since a namespace is only ever registered once.
//
// See `docs/i18n/README.md` ("Staff and admin namespaces") and
// `app/lazy-routes/staff-locales.ts` for the full mechanism.
//
// Generic over `EnResources` (`types.d.ts`) rather than `(ns: string,
// resource: Record<string, unknown>)` so a call site is checked against the
// SAME strict per-namespace shape `useTranslation(ns)` is — passing
// `evaluation.json`'s content for `ns: 'educator'`, or a namespace name
// `EnResources` doesn't know about, is now a `tsc` error at the call site
// instead of only surfacing later as a confusing runtime translation bug.
export function registerEnglishNamespace<K extends keyof EnResources>(ns: K, resource: EnResources[K]): void {
  i18next.addResourceBundle('en', ns, resource, true, true);
}

export default i18next;
