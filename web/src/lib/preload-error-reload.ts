// A lazy route chunk (`React.lazy`) can fail to load — most commonly a
// stale tab whose cached `index.html` still points at a chunk hash a newer
// deploy has since removed from the server. Vite's dynamic-import helper
// dispatches `vite:preloadError` on `window` for exactly this case (see
// https://vite.dev/guide/build.html#load-error-handling). A single reload
// almost always fixes it, since the reload fetches the CURRENT
// `index.html`/manifest; the `sessionStorage` guard stops a reload LOOP if
// the underlying problem isn't actually a stale chunk (e.g. the network is
// genuinely down) — `MainLayout`'s route-level error boundary
// (`components/layouts/main-layout.tsx`) is the user-visible fallback for
// that case instead. The guard is cleared once this load reaches the page's
// own `load` event with no further `vite:preloadError`, so a later,
// unrelated chunk failure (e.g. the next deploy) still gets its own fresh
// single retry rather than being silently suppressed for the rest of this
// tab's session.
//
// Extracted from `main.tsx` (which otherwise has no unit test of its own —
// it unconditionally initializes Sentry and mounts the real app) purely so
// this logic has something to directly unit-test against a fake
// `window`/`sessionStorage` — see `preload-error-reload.test.ts`.
export const PRELOAD_ERROR_RELOAD_KEY = 'iep-advisor:preload-error-reloaded';

/** Narrower than `Pick<Window, 'addEventListener'>` on purpose — the real
 *  overload set requires a same-named `ev` parameter a test's simple fake
 *  listener has no reason to declare. */
export interface MinimalEventTarget {
  addEventListener(type: string, listener: () => void): void;
}

export interface PreloadErrorReloadDeps {
  window: MinimalEventTarget;
  sessionStorage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;
  reload: () => void;
}

/** Installs the `vite:preloadError`/`load` listeners described above. Takes
 *  its dependencies as parameters (defaulting to the real globals) so a test
 *  can supply fakes instead of mutating `window`/`sessionStorage` directly. */
export function installPreloadErrorReload({
  window: targetWindow = window,
  sessionStorage: storage = sessionStorage,
  reload = () => window.location.reload(),
}: Partial<PreloadErrorReloadDeps> = {}): void {
  targetWindow.addEventListener('vite:preloadError', () => {
    if (storage.getItem(PRELOAD_ERROR_RELOAD_KEY) === '1') return;
    storage.setItem(PRELOAD_ERROR_RELOAD_KEY, '1');
    reload();
  });
  targetWindow.addEventListener('load', () => storage.removeItem(PRELOAD_ERROR_RELOAD_KEY));
}
