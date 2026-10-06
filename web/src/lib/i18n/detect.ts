/**
 * Language resolution for the synchronous i18next init in `index.ts` — run
 * before React ever mounts, so it has no access to a signed-in `User`
 * object, only whatever's reachable from `localStorage` right now.
 *
 * The full resolution order (design doc, Architecture → Web) is:
 *   1. the signed-in session's cached account language — the
 *      `preferredLanguage` on the last `/me` response `lib/auth` stored,
 *      read synchronously here so a page *reload* doesn't flash the browser
 *      or pre-login language before `AuthProvider` gets a chance to confirm
 *      it. A fresh (not-yet-cached) sign-in still goes through
 *      `AuthProvider.loadUser`/`syncLanguagePreference` once `/me` resolves;
 *   2. the visitor's **explicit** pre-login choice — the switcher or a
 *      `?lang=` visit while signed out (`getPreLoginLanguage`);
 *   3. the **display-only** carry-over from the previous session's sign-out
 *      (`getLastDisplayLanguage`) — so the login page doesn't flash back to
 *      the browser's language the instant someone signs out;
 *   4. the browser's `navigator.languages` (`es-*` -> `es`);
 *   5. `en`.
 *
 * Two different signals can populate "the language to show while signed
 * out," and they must never be confused with each other — a shared device
 * is why:
 *   - `getPreLoginLanguage`/`setPreLoginLanguage` — the **explicit** choice:
 *     someone actually picked this language (the switcher, or a `?lang=`
 *     visit) while no one was signed in. This is the ONLY signal that may
 *     ever be used to *backfill* a null `preferredLanguage` on sign-in (see
 *     `AuthProvider.syncLanguagePreference`) — it is, in fact, something a
 *     real visitor chose.
 *   - `getLastDisplayLanguage`/`setLastDisplayLanguage` — the **carry-over**:
 *     whatever language happened to be active when the previous user signed
 *     out. It's correct for *that* language to keep showing on the login
 *     page (nobody wants the UI to jump back to the browser's language the
 *     instant someone logs out), but it is NOT anyone's choice about what
 *     the *next* signed-in account should use — on a shared/kiosk device,
 *     the next person to sign in may have nothing to do with the language
 *     the previous one left active. Never use this to decide a backfill, and
 *     never treat it as equivalent to an explicit pre-login choice.
 *
 * Exposed as pure functions over injectable inputs so the order itself is
 * unit-testable without touching i18next or the DOM.
 */

import { getStoredUser } from '@/lib/auth';

export const SUPPORTED_LANGUAGES = ['en', 'es'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

const PRE_LOGIN_LANGUAGE_KEY = 'iep-assistant_lang_prelogin';
const LAST_DISPLAY_LANGUAGE_KEY = 'iep-assistant_lang_last_display';

export function isSupportedLanguage(value: string | null | undefined): value is SupportedLanguage {
  return !!value && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/** Normalize a BCP-47 tag like `es-MX` or `EN-us` to its supported base language. */
function normalizeToSupported(tag: string | null | undefined): SupportedLanguage | null {
  if (!tag) return null;
  const base = tag.split('-')[0].toLowerCase();
  return isSupportedLanguage(base) ? base : null;
}

/**
 * The language the visitor **explicitly** chose before signing in — the
 * switcher, or a `?lang=` visit — while no one was signed in. The only
 * pre-login signal that may ever back a null-preference backfill (see the
 * module doc comment above and `AuthProvider.syncLanguagePreference`).
 */
export function getPreLoginLanguage(): SupportedLanguage | null {
  try {
    return normalizeToSupported(localStorage.getItem(PRE_LOGIN_LANGUAGE_KEY));
  } catch {
    // Private browsing / blocked storage: fall through to browser detection.
    return null;
  }
}

/** Persist a pre-login choice (switcher, or a `?lang=` visit) for the next visit. */
export function setPreLoginLanguage(language: SupportedLanguage): void {
  try {
    localStorage.setItem(PRE_LOGIN_LANGUAGE_KEY, language);
  } catch {
    // The choice still applies for this session via i18n.changeLanguage; it
    // just won't be remembered on the next visit from this browser.
  }
}

/** Test cleanup for the explicit pre-login key — see the module doc comment. */
export function clearPreLoginLanguage(): void {
  try {
    localStorage.removeItem(PRE_LOGIN_LANGUAGE_KEY);
  } catch {
    // Nothing to clean up if storage isn't available.
  }
}

/**
 * The language that was active when the previous session signed out —
 * display-only (see the module doc comment above). Used by
 * `detectInitialLanguage` ONLY to choose what the login page shows; never a
 * basis for backfilling the next signed-in account's `preferredLanguage`.
 */
export function getLastDisplayLanguage(): SupportedLanguage | null {
  try {
    return normalizeToSupported(localStorage.getItem(LAST_DISPLAY_LANGUAGE_KEY));
  } catch {
    return null;
  }
}

/** Called by `AuthProvider.logout` to carry the active language onto the login page. */
export function setLastDisplayLanguage(language: SupportedLanguage): void {
  try {
    localStorage.setItem(LAST_DISPLAY_LANGUAGE_KEY, language);
  } catch {
    // Best effort — the login page falls back to the explicit pre-login
    // choice, then browser detection, if this isn't remembered.
  }
}

/**
 * Called by `AuthProvider` after a successful sign-in (the carry-over's job
 * is done — the language is now the account's own concern) and by test
 * cleanup, resetting storage between cases without hardcoding this module's
 * private key.
 */
export function clearLastDisplayLanguage(): void {
  try {
    localStorage.removeItem(LAST_DISPLAY_LANGUAGE_KEY);
  } catch {
    // Nothing to clean up if storage isn't available.
  }
}

/**
 * The cached account language from the last `/me` response `lib/auth`
 * stored (`localStorage`, not a fresh network call) — read synchronously so
 * it's available before the request that would otherwise confirm it (see
 * `AuthProvider.loadUser`). `null` if there's no stored session, it's
 * unparseable, or it has no supported `preferredLanguage`.
 */
function getStoredUserLanguage(): SupportedLanguage | null {
  try {
    const raw = getStoredUser();
    if (!raw) return null;
    const parsed = JSON.parse(raw) as { preferredLanguage?: string | null };
    return isSupportedLanguage(parsed.preferredLanguage) ? parsed.preferredLanguage : null;
  } catch {
    return null;
  }
}

/** The first supported language among `navigator.languages`, else `en`. */
export function detectBrowserLanguage(
  languages: readonly string[] = (typeof navigator !== 'undefined' ? navigator.languages : null) ?? []
): SupportedLanguage {
  for (const tag of languages) {
    const normalized = normalizeToSupported(tag);
    if (normalized) return normalized;
  }
  return DEFAULT_LANGUAGE;
}

/**
 * The language i18next initializes with, synchronously, before React
 * mounts: the cached account language, else an explicit pre-login choice,
 * else the sign-out display carry-over, else the browser's languages, else
 * `en`. `languages` is injectable for deterministic tests.
 *
 * Display-only resolution — never call this (or `getLastDisplayLanguage`)
 * from a backfill decision; see the module doc comment above.
 */
export function detectInitialLanguage(languages?: readonly string[]): SupportedLanguage {
  return (
    getStoredUserLanguage() ??
    getPreLoginLanguage() ??
    getLastDisplayLanguage() ??
    detectBrowserLanguage(languages)
  );
}
