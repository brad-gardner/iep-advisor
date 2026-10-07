import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — see
// `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';

const api = vi.hoisted(() => ({
  getUser: vi.fn(),
  updateUser: vi.fn(),
}));
vi.mock('../api/admin-api', () => api);

const showToast = vi.hoisted(() => vi.fn());
vi.mock('@/components/ui/toast', () => ({ useToast: () => ({ show: showToast }) }));

import { AdminUserDetail } from './admin-user-detail';

describe('AdminUserDetail in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and edit form in Spanish', async () => {
    api.getUser.mockResolvedValue({
      id: 1,
      email: 'parent@example.com',
      firstName: 'Ada',
      lastName: 'Lovelace',
      state: 'OH',
      role: 'User',
      isActive: true,
      createdAt: '2026-09-15T00:00:00.000Z',
    });

    await renderInSpanish(<AdminUserDetail />, {
      ns: 'admin',
      wrapper: ({ children }) => (
        <MemoryRouter initialEntries={['/admin/users/1']}>
          <Routes>
            <Route path="/admin/users/:id" element={children} />
          </Routes>
        </MemoryRouter>
      ),
    });

    expect(await screen.findByText('Editar usuario')).toBeInTheDocument();
    expect(screen.getByText('Detalles')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar cambios' })).toBeInTheDocument();
  });
});
