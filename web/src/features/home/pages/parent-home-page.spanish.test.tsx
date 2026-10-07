import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
import type { User } from '@/types/api';
import { makeHomeDto, makeParentHome } from '../test/fixtures';

const useAuthMock = vi.fn();
vi.mock('@/features/auth/hooks/use-auth', () => ({
  useAuth: () => useAuthMock(),
}));

const useHomeMock = vi.fn();
vi.mock('../hooks/use-home', () => ({
  useHome: () => useHomeMock(),
}));

vi.mock('@/features/children/components/dashboard-children-section', () => ({
  DashboardChildrenSection: () => <div data-testid="dashboard-children-section" />,
}));

const meetingsApi = vi.hoisted(() => ({ rsvpToMeeting: vi.fn() }));
vi.mock('@/features/meetings/api/meetings-api', () => meetingsApi);

import { ParentHomePage } from './parent-home-page';

function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    email: 'priya@example.com',
    firstName: 'Priya',
    lastName: 'Parent',
    state: 'OH',
    role: 'Parent',
    fullName: 'Priya Parent',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
    preferredLanguage: null,
    ...overrides,
  };
}

function renderPage() {
  return renderInSpanish(
    <MemoryRouter>
      <ParentHomePage />
    </MemoryRouter>
  );
}

describe('ParentHomePage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the operational sections in Spanish', async () => {
    useAuthMock.mockReturnValue({ user: makeUser() });
    useHomeMock.mockReturnValue({
      home: makeHomeDto({
        kind: 'Parent',
        parent: makeParentHome({ nextMeeting: null }),
      }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });

    await renderPage();

    expect(screen.getByText('No hay reuniones próximas programadas.')).toBeInTheDocument();
  });

  it('renders the error state with a Spanish retry button', async () => {
    useAuthMock.mockReturnValue({ user: makeUser() });
    useHomeMock.mockReturnValue({ home: null, isLoading: false, error: null, retry: vi.fn() });

    await renderPage();

    expect(screen.getByText('No se pudo cargar su inicio')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Intentar de nuevo' })).toBeInTheDocument();
  });
});
