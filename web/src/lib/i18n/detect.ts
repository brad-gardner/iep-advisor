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
 *   2. a language chosen before signing in, or on a prior visit after
 *      signing out (`localStorage`);
 *   3. the browser's `navigator.languages` (`es-*` -> `es`);
 *   4. `en`.
 *
 * Exposed as pure functions over injectable inputs so the order itself is
 * unit-testable without touching i18next or the DOM.
 */

import { getStoredUser } from '@/lib/auth';

export const SUPPORTED_LANGUAGES = ['en', 'es'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: SupportedLanguage = 'en';

const PRE_LOGIN_LANGUAGE_KEY = 'iep-assistant_lang_prelogin';

export function isSupportedLanguage(value: string | null | undefined): value is SupportedLanguage {
  return !!value && (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/** Normalize a BCP-47 tag like `es-MX` or `EN-us` to its supported base language. */
function normalizeToSupported(tag: string | null | undefined): SupportedLanguage | null {
  if (!tag) return null;
  const base = tag.split('-')[0].toLowerCase();
  return isSupportedLanguage(base) ? base : null;
}

/** The language chosen before the visitor signed in, if any (`?lang=` or the switcher). */
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

/**
 * Not called by app code — `AuthProvider.logout` now *sets* the pre-login
 * key to whatever language was active (so it survives sign-out) rather than
 * clearing it. Kept, and exported, purely as test cleanup: several specs
 * reset storage between cases without hardcoding this module's private
 * key.
 */
export function clearPreLoginLanguage(): void {
  try {
    localStorage.removeItem(PRE_LOGIN_LANGUAGE_KEY);
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
 * mounts: the cached account language, else a pre-login choice, else the
 * browser's languages, else `en`. `languages` is injectable for
 * deterministic tests.
 */
export function detectInitialLanguage(languages?: readonly string[]): SupportedLanguage {
  return getStoredUserLanguage() ?? getPreLoginLanguage() ?? detectBrowserLanguage(languages);
}
