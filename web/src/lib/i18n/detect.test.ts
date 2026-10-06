import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { removeToken, setStoredUser } from '@/lib/auth';
import {
  detectBrowserLanguage,
  detectInitialLanguage,
  getPreLoginLanguage,
  isSupportedLanguage,
  setPreLoginLanguage,
  clearPreLoginLanguage,
} from './detect';

describe('isSupportedLanguage', () => {
  it('accepts en and es', () => {
    expect(isSupportedLanguage('en')).toBe(true);
    expect(isSupportedLanguage('es')).toBe(true);
  });

  it('rejects anything else, including null/undefined', () => {
    expect(isSupportedLanguage('fr')).toBe(false);
    expect(isSupportedLanguage(null)).toBe(false);
    expect(isSupportedLanguage(undefined)).toBe(false);
    expect(isSupportedLanguage('')).toBe(false);
  });
});

describe('detectBrowserLanguage', () => {
  it('normalizes a region-qualified Spanish tag (es-MX) to es', () => {
    expect(detectBrowserLanguage(['es-MX'])).toBe('es');
  });

  it('normalizes a region-qualified English tag (en-GB) to en', () => {
    expect(detectBrowserLanguage(['en-GB'])).toBe('en');
  });

  it('picks the first supported language among several', () => {
    expect(detectBrowserLanguage(['fr-FR', 'es-AR', 'en-US'])).toBe('es');
  });

  it('falls back to en when nothing is supported', () => {
    expect(detectBrowserLanguage(['fr-FR', 'de-DE'])).toBe('en');
  });

  it('falls back to en for an empty list', () => {
    expect(detectBrowserLanguage([])).toBe('en');
  });
});

describe('pre-login language storage', () => {
  beforeEach(() => clearPreLoginLanguage());
  afterEach(() => clearPreLoginLanguage());

  it('is null until a choice is stored', () => {
    expect(getPreLoginLanguage()).toBeNull();
  });

  it('round-trips a stored choice', () => {
    setPreLoginLanguage('es');
    expect(getPreLoginLanguage()).toBe('es');
  });

  it('ignores a corrupted/unsupported stored value', () => {
    localStorage.setItem('iep-assistant_lang_prelogin', 'fr');
    expect(getPreLoginLanguage()).toBeNull();
  });
});

describe('detectInitialLanguage resolution order', () => {
  beforeEach(() => {
    clearPreLoginLanguage();
    removeToken(); // also clears the cached user `lib/auth` stores
  });
  afterEach(() => {
    clearPreLoginLanguage();
    removeToken();
  });

  it('prefers a stored pre-login choice over the browser languages', () => {
    setPreLoginLanguage('es');
    expect(detectInitialLanguage(['en-US'])).toBe('es');
  });

  it('falls back to the browser languages when nothing is stored', () => {
    expect(detectInitialLanguage(['es-MX', 'en-US'])).toBe('es');
  });

  it('falls back to en when neither a stored choice nor a supported browser language exists', () => {
    expect(detectInitialLanguage(['fr-FR'])).toBe('en');
  });

  // This is the ONLY coverage of the cached-account-language priority: it's
  // what `index.ts` relies on to show a reloaded, already-signed-in page in
  // the right language before React (and `AuthProvider.loadUser`) ever
  // mounts — `loadUser` deliberately does not re-apply it (see its doc
  // comment), so there is no other startup-time test of this ordering.
  it("prefers the signed-in session's cached account language over a pre-login choice or the browser", () => {
    setStoredUser(JSON.stringify({ preferredLanguage: 'es' }));
    setPreLoginLanguage('en');
    expect(detectInitialLanguage(['en-US'])).toBe('es');
  });

  it('falls through to a pre-login choice when the cached account has no supported preference', () => {
    setStoredUser(JSON.stringify({ preferredLanguage: null }));
    setPreLoginLanguage('es');
    expect(detectInitialLanguage(['en-US'])).toBe('es');
  });

  it('ignores an unparseable cached user record', () => {
    localStorage.setItem('iep-assistant_user', '{not json');
    setPreLoginLanguage('es');
    expect(detectInitialLanguage(['en-US'])).toBe('es');
  });
});
