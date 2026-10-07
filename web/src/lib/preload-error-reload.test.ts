import { describe, expect, it, vi, afterEach } from 'vitest';
import {
  installPreloadErrorReload,
  markBackgroundImportStart,
  markBackgroundImportEnd,
  PRELOAD_ERROR_RELOAD_KEY,
} from './preload-error-reload';

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
  };
}

/** `backgroundImportsInFlight` is module-level state shared across every
 *  test in this file (same reasoning as its own doc comment) — reset it
 *  after every test so one test's `markBackgroundImportStart()` can never
 *  leak into the next. Also clear the real `sessionStorage` key the
 *  "no override" test below writes to, so it can't leak into a later test
 *  in this same file. */
afterEach(() => {
  // Drain any count a failed assertion left behind, rather than assuming
  // tests always balance start/end themselves.
  for (let i = 0; i < 10; i++) markBackgroundImportEnd();
  sessionStorage.removeItem(PRELOAD_ERROR_RELOAD_KEY);
});

describe('installPreloadErrorReload', () => {
  it('reloads once on a vite:preloadError', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload, now: () => 0 });

    fire('vite:preloadError');

    expect(reload).toHaveBeenCalledTimes(1);
    expect(sessionStorage.getItem(PRELOAD_ERROR_RELOAD_KEY)).toBe('0');
  });

  it('error → reload → load → error within the cooldown window → exactly 1 reload', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    let time = 0;
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload, now: () => time });

    fire('vite:preloadError'); // reload #1 at t=0
    time += 1_000;
    fire('load'); // no listener installed for this anymore — must not clear anything
    time += 1_000; // still well within the 30s cooldown
    fire('vite:preloadError');
    fire('vite:preloadError');

    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('a failure after the cooldown window triggers a second reload', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    let time = 0;
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload, now: () => time });

    fire('vite:preloadError'); // reload #1 at t=0
    time += 30_001; // just past the 30s cooldown
    fire('vite:preloadError'); // reload #2

    expect(reload).toHaveBeenCalledTimes(2);
  });

  it('a background import (e.g. the rich-text editor warm-up) failing never triggers a reload', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload, now: () => 0 });

    markBackgroundImportStart();
    fire('vite:preloadError');
    expect(reload).not.toHaveBeenCalled();

    // Once the background import settles, a genuine navigation-chunk
    // failure still gets its normal single reload.
    markBackgroundImportEnd();
    fire('vite:preloadError');
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('two overlapping background imports: a reload stays suppressed until BOTH end', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const sessionStorage = makeFakeStorage();
    const reload = vi.fn();
    installPreloadErrorReload({ window: fakeWindow, sessionStorage, reload, now: () => 0 });

    markBackgroundImportStart();
    markBackgroundImportStart();
    markBackgroundImportEnd(); // only one of the two has settled
    fire('vite:preloadError');
    expect(reload).not.toHaveBeenCalled();

    markBackgroundImportEnd();
    fire('vite:preloadError');
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('a throwing sessionStorage never stops the app from installing or recovering', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const throwingStorage = {
      getItem: () => {
        throw new Error('blocked site data');
      },
      setItem: () => {
        throw new Error('blocked site data');
      },
    };
    const reload = vi.fn();

    expect(() =>
      installPreloadErrorReload({ window: fakeWindow, sessionStorage: throwingStorage, reload, now: () => 0 })
    ).not.toThrow();

    expect(() => fire('vite:preloadError')).not.toThrow();
    // Storage is unusable, so the cooldown can't be persisted — but the
    // actual recovery (the reload) still happens rather than silently
    // doing nothing.
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it('a sessionStorage reference that throws on access (not just a method call) never stops installing', () => {
    const { fakeWindow, fire } = makeFakeWindow();
    const reload = vi.fn();

    // No `sessionStorage` override passed — exercises `safeSessionStorageRef`'s
    // own try/catch around the real global. jsdom's `sessionStorage` works
    // fine here, so this mainly proves `installPreloadErrorReload()` (as
    // `main.tsx` actually calls it, with no deps) never throws synchronously
    // and that a reload still fires.
    expect(() => installPreloadErrorReload({ window: fakeWindow, reload, now: () => 0 })).not.toThrow();
    expect(() => fire('vite:preloadError')).not.toThrow();
    expect(reload).toHaveBeenCalledTimes(1);
  });
});
