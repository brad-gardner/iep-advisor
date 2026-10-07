import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import type { ReactElement } from 'react';
import i18n, { featureNamespaces } from '@/lib/i18n';

/**
 * Switches the shared i18next instance to Spanish, renders `ui`, and
 * returns the usual React Testing Library result. Tests stay English by
 * default (see `test/setup.ts`); use this only in a test that specifically
 * exercises Spanish copy.
 *
 * The instance is shared across every test in a file, so a suite that mixes
 * `renderInSpanish` with plain `render` should restore English afterward,
 * e.g.:
 * ```ts
 * afterEach(() => resetTestLanguage());
 * ```
 *
 * Every feature-level namespace (anything but the shell namespaces
 * `common`/`auth`) is preloaded in Spanish before `render` — same reasoning
 * as `test/setup.ts` preloading them all in English: a feature namespace
 * loads lazily, on demand, the first time a component calls
 * `useTranslation('<namespace>')` (see `docs/i18n/README.md`), which happens
 * only once `ui` actually mounts, i.e. AFTER this function's own `render`
 * call already returned. Without preloading, a synchronous assertion run
 * right after `await renderInSpanish(...)` can race that load and see the
 * raw `ns:key` text (or, before react-i18next re-renders, nothing at all).
 * Pass `ns` only for a namespace NOT discovered under `locales/es/*.json`
 * (there isn't one in practice — every feature namespace has its own file —
 * but the option stays for an edge case).
 */
export async function renderInSpanish(
  ui: ReactElement,
  options?: RenderOptions & { ns?: string | string[] }
): Promise<RenderResult> {
  const { ns, ...renderOptions } = options ?? {};
  await i18n.changeLanguage('es');
  await i18n.loadNamespaces(ns ? [...featureNamespaces, ...(Array.isArray(ns) ? ns : [ns])] : featureNamespaces);
  return render(ui, renderOptions);
}

/** Restores English after a test that called `renderInSpanish`. */
export async function resetTestLanguage(): Promise<void> {
  await i18n.changeLanguage('en');
}
