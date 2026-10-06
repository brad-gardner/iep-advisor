import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { User } from '@/types/api';

const setLanguage = vi.hoisted(() => vi.fn().mockResolvedValue({ success: true }));
const updateProfile = vi.hoisted(() => vi.fn());
vi.mock('../hooks/use-auth', () => ({
  useAuth: () => ({
    user: {
      id: 1,
      email: 'pat@example.com',
      firstName: 'Pat',
      lastName: 'Parent',
      state: 'OH',
      role: 'Parent',
      fullName: 'Pat Parent',
      mfaEnabled: false,
      onboardingCompleted: true,
      subscriptionStatus: 'active',
      preferredLanguage: 'es',
    } satisfies User,
    updateProfile,
    setLanguage,
  }),
}));

// Subscription and calendar cards make their own API calls on mount; stub
// those modules so the page settles without unrelated network activity.
vi.mock('@/features/subscription/api/subscription-api', () => ({
  getSubscriptionStatus: vi.fn().mockResolvedValue({ status: 'none', expiresAt: null, childUsage: {} }),
  createPortalSession: vi.fn(),
}));
vi.mock('@/features/calendar/api/calendar-api', () => ({
  getCalendarFeed: vi.fn().mockResolvedValue({ success: false, message: 'none' }),
  regenerateCalendarFeed: vi.fn(),
}));

import { ProfilePage } from './profile-page';

function renderProfilePage() {
  return renderInSpanish(
    <MemoryRouter>
      <ToastProvider>
        <ProfilePage />
      </ToastProvider>
    </MemoryRouter>
  );
}

describe('ProfilePage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading, the Language field, and account controls in Spanish', async () => {
    await renderProfilePage();

    expect((await screen.findAllByText('Perfil')).length).toBeGreaterThan(0);
    expect(screen.getByRole('heading', { name: 'Idioma' })).toBeInTheDocument();
    expect(screen.getByTestId('profile-language-switcher')).toHaveAccessibleDescription(
      'Cambia el idioma de la aplicación y de los correos electrónicos de la cuenta.'
    );
    const languageLabel = screen.getByText('Idioma', { selector: 'span[lang="es"]' }).closest('label');
    expect(languageLabel).toHaveTextContent('Idioma / Language');
    expect(screen.getByRole('button', { name: 'Guardar cambios' })).toBeInTheDocument();
    expect(screen.getByText('Verificación en dos pasos')).toBeInTheDocument();
    expect(screen.getByText('Cuenta')).toBeInTheDocument();
  });

  it('switches the active language from the Profile page and persists it to the account', async () => {
    const user = userEvent.setup();
    await renderProfilePage();

    const select = screen.getByTestId('profile-language-switcher');
    await user.selectOptions(select, 'en');

    expect(setLanguage).toHaveBeenCalledWith('en');
  });
});
