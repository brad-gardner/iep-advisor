import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { renderInSpanish, resetTestLanguage } from '@/test/i18n-test-utils';
// `admin` is a staff-only namespace (plan phase 6) — its English isn't
// bundled in `resources` (see `lib/i18n/index.ts`), only registered by this
// side-effect import, exactly as the page's real lazy route chunk
// (`app/lazy-routes/platform-admin-routes.tsx`) registers it before the page
// can render. See `docs/i18n/README.md`'s "Staff and admin namespaces".
import '@/app/lazy-routes/staff-locales';
import type { AdminDashboardStats, AdminUser } from '@/types/api';

const api = vi.hoisted(() => ({
  getDashboardStats: vi.fn(),
  getRecentUsers: vi.fn(),
}));
vi.mock('../api/admin-api', () => api);

import { AdminDashboardPage } from './admin-dashboard-page';

function stats(): AdminDashboardStats {
  return {
    totalUsers: 10,
    activeUsers: 8,
    adminUsers: 2,
    usersWithSubscription: 5,
    usersOnboarded: 7,
    totalChildren: 6,
    totalDocuments: 4,
    documentsParsed: 3,
    documentsCreated: 4,
    documentsError: 0,
    totalAnalyses: 2,
    analysesCompleted: 2,
    analysesError: 0,
    totalGoals: 1,
    totalChecklists: 1,
    checklistsCompleted: 1,
    totalAnalysisUsage: 1,
    totalMeetingPrepUsage: 1,
    totalBetaCodes: 2,
    redeemedBetaCodes: 1,
    totalSharedAccess: 0,
    newUsersLast7Days: 1,
    newDocumentsLast7Days: 1,
    analysesLast7Days: 1,
    documentsByMeetingType: {},
    goalsByCategory: {},
    childrenByDisabilityCategory: {},
    usersBySubscriptionStatus: { active: 5, none: 5 },
  };
}

function user(overrides: Partial<AdminUser> = {}): AdminUser {
  return {
    id: 1,
    email: 'parent@example.com',
    firstName: 'Ada',
    lastName: 'Lovelace',
    state: 'OH',
    role: 'User',
    isActive: true,
    createdAt: '2026-09-15T00:00:00.000Z',
    ...overrides,
  };
}

describe('AdminDashboardPage in Spanish', () => {
  afterEach(() => resetTestLanguage());

  it('renders the heading and stat labels in Spanish', async () => {
    api.getDashboardStats.mockResolvedValue(stats());
    api.getRecentUsers.mockResolvedValue([user()]);

    await renderInSpanish(<AdminDashboardPage />);

    expect(await screen.findByRole('heading', { name: 'Panel de administración' })).toBeInTheDocument();
    expect(screen.getByText('Usuarios totales')).toBeInTheDocument();
    expect(screen.getByText('Usuarios recientes')).toBeInTheDocument();
    expect(screen.getByText('Activo')).toBeInTheDocument();
  });
});
