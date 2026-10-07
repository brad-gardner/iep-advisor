import { describe, expect, it, vi } from 'vitest';
import { installPreloadErrorReload, PRELOAD_ERROR_RELOAD_KEY } from './preload-error-reload';

/** A minimal fake of the `window`/`sessionStorage` surface this module
 *  needs, so a test can fire `vite:preloadError`/`load` without touching the
 *  real jsdom `window` (other tests in the same run share it). */
function makeFakeWindow() {
  const listeners = new Map<string, (() => void)[]>();
  const fakeWindow = {
    addEventListener: (type: string, listener: () => void) => {
      const existing = listeners.get(type) ?? [];
      existing.push(listener);
      listeners.set(type, existing);
    },
  };
  const fire = (type: string) => {
    for (const listener of listeners.get(type) ?? []) listener();
  };
  return { fakeWindow, fire };
}

function makeFakeStorage() {
  const store = new Map<string, string>();
  return {
    getItem: (key: string) => store.get(key) ?? null,
    setItem: (key: string, value: string) => store.set(key, value),
    removeItem: (key: string) => store.delete(key),
  };
}

describe('installPreloadErrorReload', () => {
  it('reloads once on a vite:preloadError', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload });

    fire('vite:preloadError');

    expect(reload).toHaveBeenCalledTimes(1);
    expect(sessionStorage.getItem(PRELOAD_ERROR_RELOAD_KEY)).toBe('1');
  });

  it('does not reload a second time within the same session (guard against a reload loop)', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload });

    fire('vite:preloadError');
    fire('vite:preloadError');
    fire('vite:preloadError');

    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('clears the guard on a successful load, so a later preloadError gets its own fresh retry', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload });

    fire('vite:preloadError');
    fire('load');

    expect(sessionStorage.getItem(PRELOAD_ERROR_RELOAD_KEY)).toBeNull();

    fire('vite:preloadError');

    expect(reload).toHaveBeenCalledTimes(2);
  });
});
