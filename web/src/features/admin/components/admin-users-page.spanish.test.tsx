import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — see
// `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';

const api = vi.hoisted(() => ({
  getUsers: vi.fn(),
  inviteBetaUser: vi.fn(),
}));
vi.mock('../api/admin-api', () => api);

const showToast = vi.hoisted(() => vi.fn());
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: showToast }) }));

import { AdminUsersPage } from './admin-users-page';

describe('AdminUsersPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and primary controls in Spanish', async () => {
    api.getUsers.mockResolvedValue([
      {
        id: 1,
        email: 'parent@example.com',
        firstName: 'Ada',
        lastName: 'Lovelace',
        state: 'OH',
        role: 'User',
        isActive: true,
        createdAt: '2026-09-15T00:00:00.000Z',
      },
    ]);

    await renderInSpanish(<AdminUsersPage />, { wrapper: ({ children }) => <MemoryRouter>{children}</MemoryRouter> });

    expect(await screen.findByRole('heading', { name: 'Gestión de usuarios' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Invitar usuario beta' })).toBeInTheDocument();
    expect(await screen.findByText('Activo')).toBeInTheDocument();
  });
});
