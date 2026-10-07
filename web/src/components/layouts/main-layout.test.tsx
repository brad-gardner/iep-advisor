import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
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

import { MainLayout } from './main-layout';

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
    preferredLanguage: null,
  };
}

/** Throws on render, same shape a lazy route chunk failing to load would
 *  surface as (a render-time throw inside the `Suspense` boundary the route
 *  wraps its lazy page in — see `main-layout.tsx`'s doc comment). */
function Boom(): never {
  throw new Error('chunk failed to load');
}

describe('MainLayout', () => {
  beforeEach(() => {
    useAuthMock.mockReturnValue({ user: makeUser(), logout: vi.fn() });
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });
  });

  it('renders its children normally when nothing throws', () => {
    render(
      <MemoryRouter>
        <MainLayout>
          <p data-testid="page-content">Hello</p>
        </MainLayout>
      </MemoryRouter>
    );

    expect(screen.getByTestId('page-content')).toBeInTheDocument();
    expect(screen.queryByTestId('lazy-route-error')).not.toBeInTheDocument();
  });

  describe('when a route throws (e.g. a lazy chunk failed to load)', () => {
    // React logs the error boundary's catch to the console; silence it so
    // the expected-failure test doesn't look like unexpected noise.
    let consoleErrorSpy: ReturnType<typeof vi.spyOn>;
    beforeEach(() => {
      consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    });
    afterEach(() => {
      consoleErrorSpy.mockRestore();
    });

    it('shows a translated generic-error message with a Reload button instead of crashing the whole page', () => {
      render(
        <MemoryRouter>
          <MainLayout>
            <Boom />
          </MainLayout>
        </MemoryRouter>
      );

      const fallback = screen.getByTestId('lazy-route-error');
      expect(fallback).toHaveTextContent('Something went wrong. Please try again.');
      expect(screen.getByRole('button', { name: 'Reload' })).toBeInTheDocument();
      // The sidebar chrome around the broken content area is still intact
      // (rendered twice — a mobile drawer copy and a desktop rail copy).
      expect(screen.getAllByTestId('sidebar-logo').length).toBeGreaterThan(0);
    });

    it('reloads the page when the Reload button is clicked', () => {
      // jsdom's `window.location.reload` is non-configurable, so it can't be
      // spied on directly — replace the whole `location` object instead.
      const reload = vi.fn();
      const originalLocation = window.location;
      Object.defineProperty(window, 'location', {
        configurable: true,
        value: { ...originalLocation, reload },
      });

      render(
        <MemoryRouter>
          <MainLayout>
            <Boom />
          </MainLayout>
        </MemoryRouter>
      );

      screen.getByRole('button', { name: 'Reload' }).click();

      expect(reload).toHaveBeenCalledTimes(1);
      Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
    });
  });
});
