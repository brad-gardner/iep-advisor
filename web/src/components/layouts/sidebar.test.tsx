import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';
import { NotificationsProvider } from '@/features/notifications/stores/notifications-context';
import type { User, UserRole } from '@/types/api';

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

function makeUser(role: UserRole): User {
  return {
    id: 1,
    email: 'staff@example.com',
    firstName: 'Sam',
    lastName: 'Staff',
    state: 'OH',
    role,
    fullName: 'Sam Staff',
    onboardingCompleted: true,
    subscriptionStatus: 'active',
  };
}

function makeProfile(orgRoleId: number): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId,
    orgRoleName: 'DistrictAdmin',
    districtId: 1,
    districtName: 'Test District',
    schoolId: null,
    schoolName: null,
    isActive: true,
    stateCode: 'OH',
    title: null,
    credentials: null,
  };
}

function renderSidebar() {
  return render(
    <MemoryRouter>
      <NotificationsProvider>
        <Sidebar onLogout={() => {}} />
      </NotificationsProvider>
    </MemoryRouter>
  );
}

describe('Sidebar Administration async gate', () => {
  beforeEach(() => {
    useAuthMock.mockReset();
    useEducatorProfileMock.mockReset();
  });

  // The sidebar renders its nav twice (a mobile drawer and a desktop rail), so
  // every testid is expected to appear exactly twice.
  it('reserves a skeleton (not the real admin group) while an educator profile loads', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Educator') });
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: true });

    renderSidebar();

    // Skeleton placeholder is present; the real admin group is withheld so no
    // admin links flash before the role resolves.
    expect(screen.getAllByTestId('district-admin-nav-loading')).toHaveLength(2);
    expect(screen.queryByTestId('district-admin-nav')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-educator/admin/schools')).not.toBeInTheDocument();
  });

  it('renders the real Administration group for a district admin once resolved', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Educator') });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile(ORG_ROLE.DistrictAdmin),
      isLoading: false,
    });

    renderSidebar();

    expect(screen.queryByTestId('district-admin-nav-loading')).not.toBeInTheDocument();
    expect(screen.getAllByTestId('district-admin-nav')).toHaveLength(2);
    expect(screen.getAllByTestId('nav-educator/admin/schools')).toHaveLength(2);
  });

  it('shows no admin group (and no skeleton) for a resolved teacher', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Educator') });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile(ORG_ROLE.Teacher),
      isLoading: false,
    });

    renderSidebar();

    expect(screen.queryByTestId('district-admin-nav-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('district-admin-nav')).not.toBeInTheDocument();
    // The educator "Home" nav item is present and points at the unchanged route.
    const home = screen.getAllByTestId('nav-educator')[0];
    expect(home).toHaveTextContent('Home');
    expect(home).toHaveAttribute('href', '/educator');
  });

  it('never fires the skeleton for non-educators', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Parent') });
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });

    renderSidebar();

    expect(screen.queryByTestId('district-admin-nav-loading')).not.toBeInTheDocument();
    expect(screen.queryByTestId('district-admin-nav')).not.toBeInTheDocument();
  });

  it('includes a Compliance link in the Administration group for a district admin', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Educator') });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile(ORG_ROLE.DistrictAdmin),
      isLoading: false,
    });

    renderSidebar();

    const links = screen.getAllByTestId('nav-educator/admin/compliance');
    expect(links).toHaveLength(2);
    expect(links[0]).toHaveAttribute('href', '/educator/admin/compliance');
  });

  it('includes an Exports link in the Administration group for a district admin only', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Educator') });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile(ORG_ROLE.DistrictAdmin),
      isLoading: false,
    });

    renderSidebar();

    const links = screen.getAllByTestId('nav-educator/admin/exports');
    expect(links).toHaveLength(2);
    expect(links[0]).toHaveAttribute('href', '/educator/admin/exports');
  });

  it('hides the Exports link for a school admin', () => {
    useAuthMock.mockReturnValue({ user: makeUser('Educator') });
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile(ORG_ROLE.SchoolAdmin),
      isLoading: false,
    });

    renderSidebar();

    expect(screen.queryByTestId('nav-educator/admin/exports')).not.toBeInTheDocument();
  });

  it('truncates a long name/email in the footer and exposes the full value via title', () => {
    useAuthMock.mockReturnValue({
      user: {
        ...makeUser('Educator'),
        firstName: 'Bartholomew-Christopher',
        lastName: 'Featherstonehaugh-Worthington',
        email: 'bartholomew.christopher.featherstonehaugh@a-very-long-district-domain.example.com',
      },
    });
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });

    renderSidebar();

    const [name] = screen.getAllByText('Bartholomew-Christopher Featherstonehaugh-Worthington');
    expect(name.className).toContain('truncate');
    expect(name).toHaveAttribute(
      'title',
      'Bartholomew-Christopher Featherstonehaugh-Worthington'
    );

    const [email] = screen.getAllByText(
      'bartholomew.christopher.featherstonehaugh@a-very-long-district-domain.example.com'
    );
    expect(email.className).toContain('truncate');
    expect(email).toHaveAttribute(
      'title',
      'bartholomew.christopher.featherstonehaugh@a-very-long-district-domain.example.com'
    );
  });
});
