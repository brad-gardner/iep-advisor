// A lazy route chunk (`React.lazy`) can fail to load — most commonly a
// stale tab whose cached `index.html` still points at a chunk hash a newer
// deploy has since removed from the server. Vite's dynamic-import helper
// dispatches `vite:preloadError` on `window` for exactly this case (see
// https://vite.dev/guide/build.html#load-error-handling). A single reload
// almost always fixes it, since the reload fetches the CURRENT
// `index.html`/manifest; if it doesn't, `MainLayout`'s route-level error
// boundary (`components/layouts/main-layout.tsx`) is the user-visible
// fallback instead.
//
// EVERY dynamic `import()` in the production bundle goes through Vite's
// same `__vitePreload` helper and dispatches this same global event on
// failure — not just a navigation route's. In particular, the rich-text
// editor's idle warm-up fetch (`warmRichTextEditor` in
// `components/ui/rich-text-editor.tsx`) is a background prefetch the user
// isn't waiting on; reloading the whole page out from under someone
// because that prefetch failed would be worse than just leaving the editor
// un-warmed (it still loads normally, lazily, the first time a field is
// actually opened). `markBackgroundImportStart`/`markBackgroundImportEnd`
// below let that kind of call mark itself so a failure during it is
// ignored here — the only failures this module ever reacts to are ones
// that happen with no background import in flight, i.e. a real navigation
// chunk. (A per-URL/chunk-name allowlist was the other option considered —
// rejected because production chunk filenames are hashed and matching
// against them is brittle; a counter the background caller controls
// directly doesn't depend on build output at all.)
//
// Why a TIMESTAMP-based cooldown, not the previous design's one-shot flag
// cleared on `window`'s `load` event: `load` fires early in the page
// lifecycle — before auth resolves, and therefore before any lazy ROUTE
// chunk is ever requested, since those only import once a protected route
// actually renders (`WarmEditorOnIdle`/`RoleRoute` in `app/routes.tsx`). A
// `load`-cleared guard is therefore already clear again by the time a
// route chunk fails, so every such failure reloaded unconditionally — and
// the FRESH page's own `load` event cleared the guard again almost
// immediately, so a chunk that's genuinely, persistently missing (not just
// a one-off stale-tab deploy race) reloaded in a tight loop instead of ever
// falling through to `MainLayout`'s error boundary. Recording *when* the
// last automatic reload happened, and only reloading again once
// `RELOAD_COOLDOWN_MS` has passed, bounds that loop to at most one reload
// per window, regardless of when `load` fires relative to the failure.
export const PRELOAD_ERROR_RELOAD_KEY = 'iep-advisor:preload-error-last-reload';

const RELOAD_COOLDOWN_MS = 30_000;

/** Narrower than `Pick<Window, 'addEventListener'>` on purpose — the real
 *  overload set requires a same-named `ev` parameter a test's simple fake
 *  listener has no reason to declare. */
export interface MinimalEventTarget {
  addEventListener(type: string, listener: () => void): void;
}

export interface PreloadErrorReloadDeps {
  window: MinimalEventTarget;
  sessionStorage: Pick<Storage, 'getItem' | 'setItem'> | undefined;
  reload: () => void;
  /** Injected for tests; defaults to `Date.now`. */
  now: () => number;
}

// How many background (non-navigation) imports are currently in flight — a
// COUNTER rather than a boolean so two overlapping background imports can't
// have the first one's completion re-arm reload-eligibility while the
// second is still pending. Module-level (not a dependency of
// `installPreloadErrorReload`) because it's genuinely global process state,
// same as `warmed`/`LoadedImpl` in `rich-text-editor.tsx` — there is only
// ever one real `window` to react to.
let backgroundImportsInFlight = 0;

/**
 * Marks a non-navigation dynamic `import()` (e.g. the rich-text editor's
 * idle warm-up) as being in flight, so a `vite:preloadError` that fires
 * while it's pending is treated as expected and never triggers the
 * page-reload recovery below — only a navigation chunk's failure should do
 * that. Always pair with `markBackgroundImportEnd()` once the import
 * settles, success or failure (typically via `.finally()`).
 */
export function markBackgroundImportStart(): void {
  backgroundImportsInFlight += 1;
}

/** Pairs with `markBackgroundImportStart()` — see its doc comment. */
export function markBackgroundImportEnd(): void {
  backgroundImportsInFlight = Math.max(0, backgroundImportsInFlight - 1);
}

function readLastReloadAt(storage: PreloadErrorReloadDeps['sessionStorage']): number | null {
  if (!storage) return null;
  try {
    const raw = storage.getItem(PRELOAD_ERROR_RELOAD_KEY);
    return raw === null ? null : Number(raw);
  } catch {
    // Blocked site data (e.g. Safari's "Block All Cookies", or a private
    // window with storage disabled) can make a `sessionStorage` CALL throw,
    // not just referencing the global — treat it the same as "never
    // reloaded before" rather than letting it stop the reload recovery
    // below from running at all.
    return null;
  }
}

function writeLastReloadAt(storage: PreloadErrorReloadDeps['sessionStorage'], at: number): void {
  if (!storage) return;
  try {
    storage.setItem(PRELOAD_ERROR_RELOAD_KEY, String(at));
  } catch {
    // See `readLastReloadAt` above — proceed without persisting the
    // cooldown; worst case a later failure in the same tab reloads once
    // more than strictly necessary.
  }
}

/** Resolves the real `sessionStorage`, guarding against browsers where
 *  merely REFERENCING `window.sessionStorage` throws (not just calling a
 *  method on it) when site data is blocked. This runs at the very top of
 *  `main.tsx`, before the app renders at all — a throw here must never
 *  stop the app from mounting. */
function safeSessionStorageRef(): Pick<Storage, 'getItem' | 'setItem'> | undefined {
  try {
    return sessionStorage;
  } catch {
    return undefined;
  }
}

/** Installs the `vite:preloadError` listener described above. Takes its
 *  dependencies as parameters (defaulting to the real globals) so a test
 *  can supply fakes instead of mutating `window`/`sessionStorage` directly. */
export function installPreloadErrorReload({
  window: targetWindow = window,
  sessionStorage: storageOverride,
  reload = () => window.location.reload(),
  now = () => Date.now(),
}: Partial<PreloadErrorReloadDeps> = {}): void {
  const storage = storageOverride ?? safeSessionStorageRef();
  targetWindow.addEventListener('vite:preloadError', () => {
    if (backgroundImportsInFlight > 0) return;

    const lastReloadAt = readLastReloadAt(storage);
    if (lastReloadAt !== null && now() - lastReloadAt < RELOAD_COOLDOWN_MS) return;

    writeLastReloadAt(storage, now());
    reload();
  });
}
