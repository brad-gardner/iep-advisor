import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ORG_ROLE, type EducatorProfile } from '../types';

// Stub the self-fetching district modules so this test exercises only the
// role-branching logic, not their API calls.
vi.mock('@/features/district-admin/components/district-dashboard-tiles', () => ({
  DistrictDashboardTiles: () => <div data-testid="district-dashboard-tiles" />,
}));
vi.mock('@/features/district-admin/components/district-overview-card', () => ({
  DistrictOverviewCard: () => <div data-testid="district-overview-card" />,
}));
vi.mock('@/features/district-admin/components/setup-checklist-card', () => ({
  SetupChecklistCard: () => <div data-testid="setup-checklist-card" />,
}));

import { EducatorDashboard } from './educator-dashboard';

function makeProfile(overrides: Partial<EducatorProfile> = {}): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId: ORG_ROLE.Teacher,
    orgRoleName: 'Teacher',
    districtId: 1,
    districtName: 'Test District',
    schoolId: 1,
    schoolName: 'Test School',
    isActive: true,
    stateCode: 'OH',
    title: null,
    credentials: null,
    ...overrides,
  };
}

function renderDashboard(profile: EducatorProfile) {
  return render(
    <MemoryRouter>
      <EducatorDashboard profile={profile} />
    </MemoryRouter>
  );
}

describe('EducatorDashboard', () => {
  it('gives teachers a focused "Your students" module, not admin tiles', () => {
    renderDashboard(
      makeProfile({ orgRoleId: ORG_ROLE.Teacher, orgRoleName: 'Teacher' })
    );

    expect(
      screen.getByRole('heading', { name: /your students/i })
    ).toBeInTheDocument();
    const cta = screen.getByTestId('educator-students-caseload-link');
    expect(cta.closest('a')).toHaveAttribute('href', '/educator/students');
    expect(screen.queryByTestId('district-dashboard-tiles')).not.toBeInTheDocument();
  });

  it('gives district admins the setup, overview, and oversight tiles', () => {
    renderDashboard(
      makeProfile({
        orgRoleId: ORG_ROLE.DistrictAdmin,
        orgRoleName: 'DistrictAdmin',
        schoolName: null,
      })
    );

    expect(screen.getByTestId('setup-checklist-card')).toBeInTheDocument();
    expect(screen.getByTestId('district-overview-card')).toBeInTheDocument();
    expect(screen.getByTestId('district-dashboard-tiles')).toBeInTheDocument();
    expect(
      screen.queryByRole('heading', { name: /your students/i })
    ).not.toBeInTheDocument();
  });

  it('gives school admins the oversight tiles without district-only setup/overview', () => {
    renderDashboard(
      makeProfile({ orgRoleId: ORG_ROLE.SchoolAdmin, orgRoleName: 'SchoolAdmin' })
    );

    expect(screen.getByTestId('district-dashboard-tiles')).toBeInTheDocument();
    expect(screen.queryByTestId('setup-checklist-card')).not.toBeInTheDocument();
    expect(screen.queryByTestId('district-overview-card')).not.toBeInTheDocument();
    expect(
      screen.queryByRole('heading', { name: /your students/i })
    ).not.toBeInTheDocument();
  });
});
