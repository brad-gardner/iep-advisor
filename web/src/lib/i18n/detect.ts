/**
 * Language resolution for an anonymous (pre-login) visitor, plus the
 * supported-language vocabulary shared by the rest of `lib/i18n`.
 *
 * The full resolution order (design doc, Architecture → Web) is:
 *   1. the signed-in user's saved `preferredLanguage` (applied later, and
 *      separately, by `AuthProvider` via `i18n.changeLanguage` once `/me`
 *      resolves — it isn't knowable at synchronous init time);
 *   2. a language chosen before signing in (`localStorage`);
 *   3. the browser's `navigator.languages` (`es-*` -> `es`);
 *   4. `en`.
 *
 * Steps 2-4 are this module's job and are exposed as pure, dependency-free
 * functions so the order itself is unit-testable without touching i18next or
 * the DOM. `preLoginDetector` adapts them to the `i18next-browser-languagedetector`
 * plugin contract so the real detection still runs through that installed
 * library rather than a bespoke call in `index.ts`.
 */

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

export function clearPreLoginLanguage(): void {
  try {
    localStorage.removeItem(PRE_LOGIN_LANGUAGE_KEY);
  } catch {
    // Nothing to clean up if storage isn't available.
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
 * Resolution for an anonymous visitor: pre-login choice, else the browser's
 * languages, else `en`. `languages` is injectable for deterministic tests.
 */
export function detectInitialLanguage(languages?: readonly string[]): SupportedLanguage {
  return getPreLoginLanguage() ?? detectBrowserLanguage(languages);
}

/**
 * `i18next-browser-languagedetector` custom detector wrapping the resolution
 * above, so the real app wires detection through the installed library
 * (`index.ts` registers it via `LanguageDetector.addDetector`) instead of
 * calling `detectInitialLanguage` directly.
 */
export const preLoginDetector = {
  name: 'iep-prelogin',
  lookup(): string {
    return detectInitialLanguage();
  },
};
