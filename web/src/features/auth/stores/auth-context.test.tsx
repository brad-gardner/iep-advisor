import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { getStoredUser, removeToken, setStoredUser, setToken } from '@/lib/auth';
import {
  getPreLoginLanguage,
  setPreLoginLanguage,
  clearPreLoginLanguage,
  setLastDisplayLanguage,
  clearLastDisplayLanguage,
} from '@/lib/i18n/detect';
import i18n from '@/lib/i18n';
import type { User } from '@/types/api';

const authApi = vi.hoisted(() => ({
  login: vi.fn(),
  register: vi.fn(),
  registerDistrict: vi.fn(),
  getCurrentUser: vi.fn(),
  updateProfile: vi.fn(),
  completeOnboarding: vi.fn(),
}));
vi.mock('../api/auth-api', () => authApi);

import { AuthProvider } from './auth-context';
import { useAuth } from '../hooks/use-auth';

function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    email: 'pat@example.com',
    firstName: 'Pat',
    lastName: 'Parent',
    state: 'OH',
    role: 'Parent',
    fullName: 'Pat Parent',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
    preferredLanguage: null,
    ...overrides,
  };
}

// A minimal consumer so the test can drive `setLanguage` directly without a
// full page's unrelated UI. `probe-result` surfaces the last call's return
// value (not exposed by the real `LanguageSwitcher`, which only renders a
// generic error message) for tests that need to assert on it directly.
function LanguageProbe() {
  const { setLanguage, login, logout, user } = useAuth();
  const [lastResult, setLastResult] = useState<string | null>(null);
  return (
    <div>
      <span data-testid="probe-user">{user ? user.preferredLanguage ?? 'null' : 'signed-out'}</span>
      <span data-testid="probe-result">{lastResult ?? 'none'}</span>
      <button onClick={() => void setLanguage('es').then((r) => setLastResult(JSON.stringify(r)))}>switch</button>
      <button onClick={() => void setLanguage('en').then((r) => setLastResult(JSON.stringify(r)))}>switch-en</button>
      {/* Fake credentials: every test controls the resolved response via the
          `authApi.login` mock, so the actual field values here never matter. */}
      <button onClick={() => void login({ email: 'probe@example.com', password: 'x' })}>login</button>
      <button onClick={() => logout()}>logout</button>
    </div>
  );
}

type I18nBackend = { read: (language: string, namespace: string, cb: (err: unknown, data: unknown) => void) => void };

/** The real i18next backend plugin registered in `lib/i18n/index.ts` (the
 * `resourcesToBackend` instance whose `read` loads a Spanish namespace) —
 * used below to simulate a slow Spanish chunk load without reimplementing
 * i18next's own `changeLanguage` race protection. */
function getBackend(): I18nBackend {
  return (i18n as unknown as { services: { backendConnector: { backend: I18nBackend } } }).services.backendConnector
    .backend;
}

function renderProbe() {
  return render(
    <AuthProvider>
      <LanguageProbe />
    </AuthProvider>
  );
}

/**
 * Shadows `navigator.languages` (an own, configurable property on the
 * instance, overriding the prototype getter jsdom defines) for a single
 * test's "browser language" — restore it afterward so later tests see
 * jsdom's own default again.
 */
function stubBrowserLanguages(languages: string[]): () => void {
  Object.defineProperty(window.navigator, 'languages', {
    configurable: true,
    get: () => languages,
  });
  return () => {
    delete (window.navigator as unknown as { languages?: unknown }).languages;
  };
}

describe('AuthProvider.setLanguage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    removeToken();
    clearPreLoginLanguage();
  });

  afterEach(async () => {
    removeToken();
    clearPreLoginLanguage();
    await i18n.changeLanguage('en');
  });

  it('stores the choice to localStorage (not the account) when signed out', async () => {
    const user = userEvent.setup();
    renderProbe();
    await screen.findByText('signed-out');

    await user.click(screen.getByRole('button', { name: 'switch' }));

    await waitFor(() => expect(getPreLoginLanguage()).toBe('es'));
    expect(authApi.updateProfile).not.toHaveBeenCalled();
    expect(i18n.language).toBe('es');
  });

  it('fails without persisting when the language load resolves but reverts (resolvedLanguage !== requested)', async () => {
    const user = userEvent.setup();
    renderProbe();
    await screen.findByText('signed-out');

    // Simulate `changeLanguage('es')` resolving (no throw) without actually
    // switching — e.g. the lazy Spanish chunk failed and `index.ts`'s
    // `failedLoading` handler reverted to English before this resolved.
    // `i18n.resolvedLanguage` therefore still reads 'en', not the requested
    // 'es'.
    const changeLanguageSpy = vi.spyOn(i18n, 'changeLanguage').mockResolvedValue(i18n.t);

    await user.click(screen.getByRole('button', { name: 'switch' }));

    await waitFor(() => expect(screen.getByTestId('probe-result')).toHaveTextContent('"success":false'));

    changeLanguageSpy.mockRestore();
    expect(getPreLoginLanguage()).toBeNull(); // never persisted
    expect(authApi.updateProfile).not.toHaveBeenCalled();
  });

  it('PUTs the choice to the account when signed in', async () => {
    const signedInUser = makeUser({ preferredLanguage: 'en' });
    setToken('a-jwt');
    setStoredUser(JSON.stringify(signedInUser));
    authApi.getCurrentUser.mockResolvedValue({ success: true, data: signedInUser });
    authApi.updateProfile.mockResolvedValue({
      success: true,
      data: { ...signedInUser, preferredLanguage: 'es' },
    });

    const user = userEvent.setup();
    renderProbe();
    await screen.findByText('en'); // loadUser resolved, preference already 'en' (no background PUT races this)

    await user.click(screen.getByRole('button', { name: 'switch' }));

    await waitFor(() =>
      expect(authApi.updateProfile).toHaveBeenCalledWith({ preferredLanguage: 'es' })
    );
    await screen.findByText('es');
    expect(getPreLoginLanguage()).toBeNull(); // the account, not localStorage, is the record of truth
  });
});

describe('language sync races', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    removeToken();
    clearPreLoginLanguage();
  });

  afterEach(async () => {
    removeToken();
    clearPreLoginLanguage();
    await i18n.changeLanguage('en');
  });

  it('switching es then en quickly persists and shows only the last choice, even if the es chunk resolves after en', async () => {
    const user = userEvent.setup();
    renderProbe();
    await screen.findByText('signed-out');

    // Earlier tests in this file may have already loaded the Spanish
    // namespaces into i18next's resource store, which would make the next
    // `changeLanguage('es')` resolve from cache instead of going through
    // the backend at all. Evict them (directly, not via
    // `removeResourceBundle` — that also drops the namespace from the
    // shared `ns` list) so this test gets a fresh load to gate.
    const esResources = i18n.store.data.es as { common?: unknown; auth?: unknown } | undefined;
    if (esResources) {
      delete esResources.common;
      delete esResources.auth;
    }

    // Hold the Spanish namespace load open until released, so its
    // `changeLanguage('es')` call is still pending when `changeLanguage('en')`
    // starts and finishes — the scenario a quick "es, then en" switch
    // produces in practice, since English never needs this backend (it's
    // bundled). `common` *and* `auth` each get their own `backend.read`
    // call, so every one of them gets its own gate — releasing only the
    // last-registered one would leave the other's `loadResources` callback
    // permanently pending, wedging i18next's internal load state for every
    // later test that also switches to Spanish.
    const backend = getBackend();
    const originalRead = backend.read.bind(backend);
    const releaseEsFns: (() => void)[] = [];
    const readSpy = vi.spyOn(backend, 'read').mockImplementation((language, namespace, cb) => {
      if (language === 'es') {
        const gate = new Promise<void>((resolve) => {
          releaseEsFns.push(resolve);
        });
        void gate.then(() => originalRead(language, namespace, cb));
        return;
      }
      originalRead(language, namespace, cb);
    });

    await user.click(screen.getByRole('button', { name: 'switch' })); // es — its backend reads are now gated
    await user.click(screen.getByRole('button', { name: 'switch-en' })); // en — resolves immediately (bundled)

    await waitFor(() => expect(i18n.language).toBe('en'));
    expect(getPreLoginLanguage()).toBe('en');

    // Now let the stale 'es' load finish. i18next's own `changeLanguage`
    // guard (isLanguageChangingTo) keeps the active language at 'en'; this
    // proves AuthProvider also never persists the superseded 'es' choice.
    await waitFor(() => expect(releaseEsFns.length).toBe(2)); // both 'common' and 'auth' reads started
    releaseEsFns.forEach((release) => release());
    // Let the released dynamic import + its callback chain fully settle.
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(i18n.language).toBe('en');
    expect(getPreLoginLanguage()).toBe('en');
    expect(authApi.updateProfile).not.toHaveBeenCalled();

    readSpy.mockRestore();
  });

  it('keeps an explicit switch to Spanish when an in-flight /me resolves afterward with a stale English preference', async () => {
    const signedInUser = makeUser({ preferredLanguage: 'en' });
    setToken('a-jwt');
    setStoredUser(JSON.stringify(signedInUser));

    let resolveMe!: (value: { success: true; data: User }) => void;
    authApi.getCurrentUser.mockImplementation(
      () => new Promise((resolve) => { resolveMe = resolve; })
    );
    authApi.updateProfile.mockResolvedValue({
      success: true,
      data: { ...signedInUser, preferredLanguage: 'es' },
    });

    const user = userEvent.setup();
    renderProbe();

    // loadUser's /me call is in flight (gated above). Switch explicitly
    // before it resolves, letting its whole chain (changeLanguage + the
    // account PUT) settle first.
    await user.click(screen.getByRole('button', { name: 'switch' }));
    await waitFor(() => expect(i18n.language).toBe('es'));
    await waitFor(() => expect(authApi.updateProfile).toHaveBeenCalledWith({ preferredLanguage: 'es' }));

    // Only now does the in-flight /me resolve, with the account's old
    // (pre-switch) preference. Give its continuation (setUser +
    // syncLanguagePreference) a tick to run.
    resolveMe({ success: true, data: signedInUser });
    await new Promise((resolve) => setTimeout(resolve, 0));

    // It must never revert the UI to English.
    expect(i18n.language).toBe('es');
  });

  it('keeps an explicit switch when it races a background PUT for a null (first-sign-in) preference, sending it last', async () => {
    const newUser = makeUser({ preferredLanguage: null });
    setToken('a-jwt');
    setStoredUser(JSON.stringify(newUser));
    authApi.getCurrentUser.mockResolvedValue({ success: true, data: newUser });

    let resolveBackgroundPut!: (value: { success: true; data: User }) => void;
    authApi.updateProfile
      .mockImplementationOnce(
        () => new Promise((resolve) => { resolveBackgroundPut = resolve; })
      )
      .mockResolvedValue({ success: true, data: { ...newUser, preferredLanguage: 'es' } });

    const user = userEvent.setup();
    renderProbe();

    // loadUser resolves with a null preference, kicking off
    // `syncLanguagePreference`'s background PUT (gated above) before the
    // user does anything.
    await waitFor(() => expect(authApi.updateProfile).toHaveBeenCalledTimes(1));

    // `setLanguage` now deliberately awaits that in-flight backfill before
    // sending its own PUT (see `AuthProvider.setLanguage`'s
    // `pendingBackfillRef` await), so the explicit choice is guaranteed to
    // be the LAST write the server sees rather than merely racing it.
    // Resolve the backfill (with its stale, pre-switch resolved language)
    // before clicking, so `setLanguage`'s await has something to resolve to.
    resolveBackgroundPut({ success: true, data: { ...newUser, preferredLanguage: 'en' } });

    await user.click(screen.getByRole('button', { name: 'switch' }));
    await waitFor(() => expect(screen.getByTestId('probe-user')).toHaveTextContent('es'));

    expect(authApi.updateProfile).toHaveBeenCalledTimes(2);
    expect(authApi.updateProfile).toHaveBeenNthCalledWith(1, { preferredLanguage: 'en' });
    expect(authApi.updateProfile).toHaveBeenNthCalledWith(2, { preferredLanguage: 'es' });
    expect(i18n.language).toBe('es');
    expect(getPreLoginLanguage()).toBeNull();
  });
});

describe('shared-device language carry-over (logout -> sign-in)', () => {
  let restoreBrowserLanguages: () => void;

  beforeEach(() => {
    vi.clearAllMocks();
    removeToken();
    clearPreLoginLanguage();
    clearLastDisplayLanguage();
    restoreBrowserLanguages = stubBrowserLanguages(['en-US']); // the next signed-in user's "browser"
  });

  afterEach(async () => {
    removeToken();
    clearPreLoginLanguage();
    clearLastDisplayLanguage();
    restoreBrowserLanguages();
    await i18n.changeLanguage('en');
  });

  it("does not backfill the next signed-in user's null preference with the previous user's sign-out carry-over", async () => {
    // User A (Spanish) signs out: `AuthProvider.logout` records the
    // carry-over for *display* only — never the explicit pre-login key.
    await i18n.changeLanguage('es');
    setLastDisplayLanguage('es');
    expect(getPreLoginLanguage()).toBeNull();

    // User B — no saved preference, English browser — signs in.
    const userB = makeUser({ id: 2, email: 'bea@example.com', preferredLanguage: null });
    setToken('b-jwt');
    setStoredUser(JSON.stringify(userB));
    authApi.getCurrentUser.mockResolvedValue({ success: true, data: userB });
    authApi.updateProfile.mockResolvedValue({ success: true, data: { ...userB, preferredLanguage: 'en' } });

    renderProbe();

    await waitFor(() =>
      expect(authApi.updateProfile).toHaveBeenCalledWith({ preferredLanguage: 'en' })
    );
  });

  it("backfills with an explicit pre-login choice made before the next user signs in", async () => {
    await i18n.changeLanguage('es');
    setLastDisplayLanguage('es'); // the previous user's carry-over — still irrelevant to the backfill below
    setPreLoginLanguage('es'); // someone explicitly switched to Spanish on this device before B signed in

    const userB = makeUser({ id: 2, email: 'bea@example.com', preferredLanguage: null });
    setToken('b-jwt');
    setStoredUser(JSON.stringify(userB));
    authApi.getCurrentUser.mockResolvedValue({ success: true, data: userB });
    authApi.updateProfile.mockResolvedValue({ success: true, data: { ...userB, preferredLanguage: 'es' } });

    renderProbe();

    await waitFor(() =>
      expect(authApi.updateProfile).toHaveBeenCalledWith({ preferredLanguage: 'es' })
    );
  });
});

describe('todos/247: explicit pre-login choice is a one-time, one-visitor signal', () => {
  let restoreBrowserLanguages: () => void;

  beforeEach(() => {
    vi.clearAllMocks();
    removeToken();
    clearPreLoginLanguage();
    clearLastDisplayLanguage();
  });

  afterEach(async () => {
    removeToken();
    clearPreLoginLanguage();
    clearLastDisplayLanguage();
    restoreBrowserLanguages?.();
    await i18n.changeLanguage('en');
  });

  it("clears the explicit pre-login choice after sign-in, so a later null-preference account on the same device backfills from the browser, not the previous visitor's choice", async () => {
    setPreLoginLanguage('es'); // someone on this (possibly shared) device explicitly chose Spanish pre-login
    restoreBrowserLanguages = stubBrowserLanguages(['en-US']); // user B's browser, used only for B's backfill below

    const userA = makeUser({ id: 1, email: 'a@example.com', preferredLanguage: null });
    authApi.login.mockResolvedValueOnce({ success: true, data: { token: 'a-jwt', user: userA } });
    authApi.updateProfile.mockResolvedValueOnce({
      success: true,
      data: { ...userA, preferredLanguage: 'es' },
    });

    const user = userEvent.setup();
    renderProbe();
    await screen.findByText('signed-out');

    // User A signs in: the pre-login 'es' backs A's own backfill (expected —
    // it really was chosen on this device before anyone signed in)...
    await user.click(screen.getByRole('button', { name: 'login' }));
    await waitFor(() =>
      expect(authApi.updateProfile).toHaveBeenNthCalledWith(1, { preferredLanguage: 'es' })
    );
    // ...and is cleared immediately after, right after `syncLanguagePreference`
    // read it — not before (it must still be there for A's own backfill to see).
    await waitFor(() => expect(getPreLoginLanguage()).toBeNull());

    await user.click(screen.getByRole('button', { name: 'logout' }));
    await screen.findByText('signed-out');

    // User B signs in next, on the same device, with no saved preference.
    const userB = makeUser({ id: 2, email: 'b@example.com', preferredLanguage: null });
    authApi.login.mockResolvedValueOnce({ success: true, data: { token: 'b-jwt', user: userB } });
    authApi.updateProfile.mockResolvedValueOnce({
      success: true,
      data: { ...userB, preferredLanguage: 'en' },
    });

    await user.click(screen.getByRole('button', { name: 'login' }));

    // B's backfill must use the browser ('en'), never A's leftover 'es'.
    await waitFor(() =>
      expect(authApi.updateProfile).toHaveBeenNthCalledWith(2, { preferredLanguage: 'en' })
    );
  });

  it('leaves the user signed out, with nothing written to the stored user, when logout runs while a language PUT is still in flight', async () => {
    const signedInUser = makeUser({ preferredLanguage: 'en' });
    setToken('a-jwt');
    setStoredUser(JSON.stringify(signedInUser));
    authApi.getCurrentUser.mockResolvedValue({ success: true, data: signedInUser });

    let resolvePut!: (value: { success: true; data: User }) => void;
    authApi.updateProfile.mockImplementation(
      () => new Promise((resolve) => { resolvePut = resolve; })
    );

    const user = userEvent.setup();
    renderProbe();
    await screen.findByText('en'); // loadUser resolved

    await user.click(screen.getByRole('button', { name: 'switch' })); // setLanguage('es') — PUT held open
    await waitFor(() => expect(authApi.updateProfile).toHaveBeenCalledWith({ preferredLanguage: 'es' }));

    // Sign out while that PUT is still unresolved.
    await user.click(screen.getByRole('button', { name: 'logout' }));
    await screen.findByText('signed-out');
    expect(getStoredUser()).toBeNull(); // logout's removeToken() already cleared it

    // Now let the slow PUT resolve, with a signed-in-looking payload.
    resolvePut({ success: true, data: { ...signedInUser, preferredLanguage: 'es' } });
    await new Promise((resolve) => setTimeout(resolve, 0));

    // Must still be signed out — the stale response must not resurrect a
    // user with no token, nor write one back to storage.
    expect(screen.getByTestId('probe-user')).toHaveTextContent('signed-out');
    expect(getStoredUser()).toBeNull();
  });
});
