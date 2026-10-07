// Cross-audience Spanish smoke test (multilingual plan, phase 7): renders the
// real app shell (`MainLayout` — sidebar nav, notifications bell) together
// with one representative page per audience, in Spanish, and asserts the
// shell nav AND the page content both show Spanish text with no raw
// `namespace:key.path` leaking through. Per-page Spanish tests already cover
// each of these pages in isolation (see e.g.
// `src/features/home/pages/parent-home-page.spanish.test.tsx`,
// `src/features/educator/pages/educator-students-page.test.tsx`,
// `src/features/district-admin/pages/compliance-board-page.spanish.test.tsx`,
// `src/features/admin/components/admin-users-page.spanish.test.tsx`); this
// file's job is only to prove the SHELL and a PAGE coexist correctly in
// Spanish — the thing none of those isolated tests exercises.
//
// `educator`, `district-admin` and `admin` are staff/admin namespaces (see
// `docs/i18n/README.md`'s "Staff and admin namespaces") — their English is
// registered by this side-effect import, exactly as the real lazy route
// chunks register it, before any of their pages can render.
import '@/app/lazy-routes/staff-locales';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { renderInSpanish, resetTestLanguage } from './i18n-test-utils';
import { ToastProvider } from '@/components/ui/toast';
import { MainLayout } from '@/components/layouts/main-layout';
import { ORG_ROLE } from '@/features/educator/types';
import { makeProfile, makeStudent } from '@/features/educator/test/fixtures';
import { makeHomeDto, makeParentHome } from '@/features/home/test/fixtures';
import type { ComplianceBoardDto } from '@/features/district-admin/types';
import type { User } from '@/types/api';

const useAuthMock = vi.fn();
vi.mock('@/features/auth/hooks/use-auth', () => ({
  useAuth: () => useAuthMock(),
}));

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const notificationsApi = vi.hoisted(() => ({
  listNotifications: vi.fn(),
  markNotificationRead: vi.fn(),
}));
vi.mock('@/features/notifications/api/notifications-api', () => notificationsApi);

const useHomeMock = vi.fn();
vi.mock('@/features/home/hooks/use-home', () => ({
  useHome: () => useHomeMock(),
}));
vi.mock('@/features/children/components/dashboard-children-section', () => ({
  DashboardChildrenSection: () => <div data-testid="dashboard-children-section" />,
}));

const educatorApi = vi.hoisted(() => ({
  searchStudents: vi.fn(),
  createStudent: vi.fn(),
  assignCaseManagerBulk: vi.fn(),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

const districtApi = vi.hoisted(() => ({
  getDistrictSchools: vi.fn(),
  getDistrictDashboard: vi.fn(),
  getComplianceBoard: vi.fn(),
  getAdoption: vi.fn(),
  getEngagement: vi.fn(),
}));
vi.mock('@/features/district-admin/api/district-api', () => districtApi);

const staffInvitesApi = vi.hoisted(() => ({ getStaffList: vi.fn() }));
vi.mock('@/features/staff-invites/api/staff-invites-api', () => staffInvitesApi);

const adminApi = vi.hoisted(() => ({ getUsers: vi.fn(), inviteBetaUser: vi.fn() }));
vi.mock('@/features/admin/api/admin-api', () => adminApi);

import { ParentHomePage } from '@/features/home/pages/parent-home-page';
import { EducatorStudentsPage } from '@/features/educator/pages/educator-students-page';
import { ComplianceBoardPage } from '@/features/district-admin/pages/compliance-board-page';
import { AdminUsersPage } from '@/features/admin/components/admin-users-page';

function makeUser(overrides: Partial<User> = {}): User {
  return {
    id: 1,
    email: 'ada@example.com',
    firstName: 'Ada',
    lastName: 'Lovelace',
    state: 'OH',
    role: 'Parent',
    fullName: 'Ada Lovelace',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
    preferredLanguage: 'es',
    ...overrides,
  };
}

function makeBoard(): ComplianceBoardDto {
  return {
    generatedAt: '2026-09-16T00:00:00.000Z',
    from: '2026-09-16',
    to: '2026-11-15',
    summary: {
      overdueAnnual: 3,
      overdueReeval: 1,
      due30: 5,
      due60: 8,
      unknownDates: 2,
      noLead: 4,
      activeStudents: 100,
      dueInRange: 6,
    },
    bySchool: [],
    drill: {
      overdueAnnual: 'attention=OverdueAnnual',
      overdueReeval: 'attention=OverdueReeval',
      due30: 'attention=Due30',
      due60: 'attention=Due60',
      unknownDates: 'attention=UnknownDates',
      noLead: 'attention=NoCaseManager',
      dueInRange: 'attention=DueInRange&from=2026-09-16&to=2026-11-15',
    },
  };
}

/** No raw `namespace:key.path` text anywhere in the rendered shell + page —
 *  what a missing or mistranslated key leaves behind. `test/setup.ts`'s
 *  `missingKeyHandler` already throws on a genuinely missing key during
 *  render (see `docs/i18n/README.md`'s "No raw key leaking through"), so this
 *  is a belt-and-suspenders content check across the whole combined tree,
 *  not the only thing standing between a typo and a passing test. */
function expectNoRawTranslationKeys() {
  expect(document.body.textContent).not.toMatch(/\b[a-z][a-z-]*:[A-Za-z][\w.]*\b/);
}

describe('Spanish cross-audience smoke (app shell + one page per audience)', () => {
  afterEach(async () => {
    vi.clearAllMocks();
    await resetTestLanguage();
  });

  it('parent dashboard: shell nav and page content both render in Spanish', async () => {
    useAuthMock.mockReturnValue({ user: makeUser({ role: 'Parent' }), logout: vi.fn(), setLanguage: vi.fn() });
    notificationsApi.listNotifications.mockResolvedValue({ success: true, data: { items: [], unreadCount: 0 } });
    // The Sidebar calls `useEducatorProfile` unconditionally (only Educators
    // actually need it — see `sidebar.tsx`'s own `enabled` flag), so even a
    // non-Educator audience's test must give the mocked hook something to
    // return.
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });
    useHomeMock.mockReturnValue({
      home: makeHomeDto({ kind: 'Parent', parent: makeParentHome({ nextMeeting: null }) }),
      isLoading: false,
      error: null,
      retry: vi.fn(),
    });

    await renderInSpanish(
      <MemoryRouter>
        <ToastProvider>
          <MainLayout>
            <ParentHomePage />
          </MainLayout>
        </ToastProvider>
      </MemoryRouter>
    );

    // Shell: the sidebar's own nav, in Spanish.
    expect(screen.getAllByText('Inicio')[0]).toBeInTheDocument();
    // Page: the parent home's welcome heading, in Spanish.
    expect(await screen.findByRole('heading', { name: 'Le damos la bienvenida, Ada' })).toBeInTheDocument();
    expectNoRawTranslationKeys();
  });

  it('staff students list: shell nav and page content both render in Spanish', async () => {
    useAuthMock.mockReturnValue({ user: makeUser({ role: 'Educator' }), logout: vi.fn(), setLanguage: vi.fn() });
    notificationsApi.listNotifications.mockResolvedValue({ success: true, data: { items: [], unreadCount: 0 } });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ orgRoleId: ORG_ROLE.Teacher, orgRoleName: 'Teacher' }),
      isLoading: false,
    });
    educatorApi.searchStudents.mockResolvedValue({
      success: true,
      data: { items: [makeStudent({ id: 1, firstName: 'Ada', lastName: 'Lovelace' })], total: 1, page: 1, pageSize: 50 },
    });
    districtApi.getDistrictSchools.mockResolvedValue({ success: true, data: [] });
    districtApi.getDistrictDashboard.mockResolvedValue({ success: true, data: null });
    staffInvitesApi.getStaffList.mockResolvedValue({ success: true, data: { members: [], pendingInvites: [] } });

    await renderInSpanish(
      <MemoryRouter initialEntries={['/educator/students']}>
        <ToastProvider>
          <MainLayout>
            <EducatorStudentsPage />
          </MainLayout>
        </ToastProvider>
      </MemoryRouter>,
      { ns: 'educator' }
    );

    expect(screen.getAllByText('Inicio')[0]).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Estudiantes', level: 1 })).toBeInTheDocument();
    expectNoRawTranslationKeys();
  });

  it('district compliance board: shell nav and page content both render in Spanish', async () => {
    useAuthMock.mockReturnValue({ user: makeUser({ role: 'Educator' }), logout: vi.fn(), setLanguage: vi.fn() });
    notificationsApi.listNotifications.mockResolvedValue({ success: true, data: { items: [], unreadCount: 0 } });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ orgRoleId: ORG_ROLE.DistrictAdmin, orgRoleName: 'DistrictAdmin' }),
      isLoading: false,
    });
    districtApi.getComplianceBoard.mockResolvedValue({ success: true, data: makeBoard() });
    districtApi.getDistrictSchools.mockResolvedValue({ success: true, data: [] });
    districtApi.getAdoption.mockResolvedValue({
      success: true,
      data: {
        days: 14,
        staffActiveLast14: 8,
        staffTotal: 10,
        bySchool: [],
        draftsStarted: 4,
        draftsFinalized: 2,
        activeRule: 'Logged in or edited a document in the window',
      },
    });
    districtApi.getEngagement.mockResolvedValue({
      success: true,
      data: { studentsWithFamilyLink: 40, activeStudents: 60, draftsShared: 0, responsesReceived: 0, bySchool: [] },
    });

    await renderInSpanish(
      <MemoryRouter>
        <ToastProvider>
          <MainLayout>
            <ComplianceBoardPage />
          </MainLayout>
        </ToastProvider>
      </MemoryRouter>,
      { ns: 'district-admin' }
    );

    expect(screen.getAllByText('Inicio')[0]).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Cumplimiento' })).toBeInTheDocument();
    expectNoRawTranslationKeys();
  });

  it('platform admin users: shell nav and page content both render in Spanish', async () => {
    useAuthMock.mockReturnValue({ user: makeUser({ role: 'Admin' }), logout: vi.fn(), setLanguage: vi.fn() });
    notificationsApi.listNotifications.mockResolvedValue({ success: true, data: { items: [], unreadCount: 0 } });
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });
    adminApi.getUsers.mockResolvedValue([]);

    await renderInSpanish(
      <MemoryRouter>
        <ToastProvider>
          <MainLayout>
            <AdminUsersPage />
          </MainLayout>
        </ToastProvider>
      </MemoryRouter>,
      { ns: 'admin' }
    );

    expect(screen.getAllByText('Inicio')[0]).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Gestión de usuarios' })).toBeInTheDocument();
    expectNoRawTranslationKeys();
  });
});
