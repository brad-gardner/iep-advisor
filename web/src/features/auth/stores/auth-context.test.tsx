import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { removeToken, setStoredUser, setToken } from '@/lib/auth';
import { getPreLoginLanguage, clearPreLoginLanguage } from '@/lib/i18n/detect';
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
// full page's unrelated UI.
function LanguageProbe() {
  const { setLanguage, user } = useAuth();
  return (
    <div>
      <span data-testid="probe-user">{user ? user.preferredLanguage ?? 'null' : 'signed-out'}</span>
      <button onClick={() => void setLanguage('es')}>switch</button>
      <button onClick={() => void setLanguage('en')}>switch-en</button>
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

  it('keeps an explicit switch when it races a background PUT for a null (first-sign-in) preference', async () => {
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

    // The explicit switch's own PUT resolves normally and wins.
    await user.click(screen.getByRole('button', { name: 'switch' }));
    await waitFor(() => expect(screen.getByTestId('probe-user')).toHaveTextContent('es'));

    // Only now does the background PUT (for the stale, pre-switch resolved
    // language) resolve. It must not clobber the explicit choice.
    resolveBackgroundPut({ success: true, data: { ...newUser, preferredLanguage: 'en' } });
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(screen.getByTestId('probe-user')).toHaveTextContent('es');
    expect(i18n.language).toBe('es');
    expect(getPreLoginLanguage()).toBeNull();
  });
});
