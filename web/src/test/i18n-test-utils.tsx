import { render, type RenderOptions, type RenderResult } from '@testing-library/react';
import type { ReactElement } from 'react';
import i18n from '@/lib/i18n';

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
 */
export async function renderInSpanish(ui: ReactElement, options?: RenderOptions): Promise<RenderResult> {
  await i18n.changeLanguage('es');
  return render(ui, options);
}

/** Restores English after a test that called `renderInSpanish`. */
export async function resetTestLanguage(): Promise<void> {
  await i18n.changeLanguage('en');
}
