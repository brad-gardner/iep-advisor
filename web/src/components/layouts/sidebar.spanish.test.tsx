import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { NotificationsProvider } from '@/features/notifications/stores/notifications-context';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { User } from '@/types/api';

const useAuthMock = vi.fn();
const useEducatorProfileMock = vi.fn();

vi.mock('@/features/auth/hooks/use-auth', () => ({
  useAuth: () => useAuthMock(),
}));
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));
vi.mock('@/features/notifications/api/notifications-api', () => ({
  listNotifications: vi.fn().mockResolvedValue({ success: true, data: { items: [], unreadCount: 0 } }),
  markNotificationRead: vi.fn(),
}));

import { Sidebar } from './sidebar';

function makeUser(): User {
  return {
    id: 1,
    email: 'parent@example.com',
    firstName: 'Pat',
    lastName: 'Parent',
    state: 'OH',
    role: 'Parent',
    fullName: 'Pat Parent',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
    preferredLanguage: 'es',
  };
}

describe('Sidebar nav in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the parent nav labels, Support, and Sign Out in Spanish', async () => {
    useAuthMock.mockReturnValue({ user: makeUser(), setLanguage: vi.fn() });
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });

    await renderInSpanish(
      <MemoryRouter>
        <NotificationsProvider>
          <Sidebar onLogout={() => {}} />
        </NotificationsProvider>
      </MemoryRouter>
    );

    expect(screen.getAllByText('Inicio')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Mis hijos')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Perfil')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Centro de recursos')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Soporte')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Cerrar sesión')[0]).toBeInTheDocument();
    expect(screen.getAllByText('Confianza y privacidad')[0]).toBeInTheDocument();

    expect(document.body.textContent).not.toMatch(/common:[a-zA-Z.]+/);
  });
});
