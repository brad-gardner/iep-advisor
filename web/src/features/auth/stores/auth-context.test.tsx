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
    </div>
  );
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
