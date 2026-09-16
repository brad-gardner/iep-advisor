import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';

const useEducatorProfileMock = vi.fn();

vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));
vi.mock('../components/staff-home-body', () => ({
  StaffHomeBody: () => <div data-testid="staff-home-body" />,
}));

import { StaffHomePage } from './staff-home-page';

function makeProfile(overrides: Partial<EducatorProfile> = {}): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId: ORG_ROLE.DistrictAdmin,
    orgRoleName: 'DistrictAdmin',
    districtId: 1,
    districtName: 'Test District',
    schoolId: null,
    schoolName: null,
    isActive: true,
    stateCode: 'OH',
    title: null,
    credentials: null,
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <StaffHomePage />
    </MemoryRouter>
  );
}

describe('StaffHomePage', () => {
  beforeEach(() => {
    useEducatorProfileMock.mockReset();
  });

  it('shows a loading status (no wrong-home flash) while the profile resolves', () => {
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: true });
    renderPage();

    expect(screen.getByRole('status', { name: /loading your home/i })).toBeInTheDocument();
    expect(screen.queryByTestId('staff-home-body')).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
    expect(document.title).toBe('Home · IEP Advisor');
  });

  it('puts the org identity in the header, sets the tab title, and shows the home body', () => {
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ orgRoleName: 'DistrictAdmin', stateCode: 'OH' }),
      isLoading: false,
    });
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Test District' })).toBeInTheDocument();
    expect(screen.getByText('District administrator · OH')).toBeInTheDocument();
    const action = screen.getByTestId('educator-students-link');
    expect(action.closest('a')).toHaveAttribute('href', '/educator/students');
    expect(screen.getByTestId('staff-home-body')).toBeInTheDocument();
    expect(document.title).toBe('Test District · IEP Advisor');
  });

  it('prefers the school name in the header for school-scoped staff', () => {
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({
        orgRoleId: ORG_ROLE.Teacher,
        orgRoleName: 'Teacher',
        schoolName: 'Lincoln Elementary',
      }),
      isLoading: false,
    });
    renderPage();

    expect(screen.getByRole('heading', { level: 1, name: 'Lincoln Elementary' })).toBeInTheDocument();
  });

  it('keeps the no-profile support notice intact', () => {
    useEducatorProfileMock.mockReturnValue({ profile: null, isLoading: false });
    renderPage();

    expect(screen.getByTestId('educator-no-profile')).toBeInTheDocument();
    expect(screen.queryByTestId('educator-students-link')).not.toBeInTheDocument();
  });

  it('shows the deactivated notice for an inactive profile', () => {
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ isActive: false }),
      isLoading: false,
    });
    renderPage();

    expect(screen.queryByTestId('staff-home-body')).not.toBeInTheDocument();
    expect(screen.queryByTestId('educator-students-link')).not.toBeInTheDocument();
  });
});
