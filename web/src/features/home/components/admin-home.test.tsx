import { describe, it, expect } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { vi } from 'vitest';
import {
  makeCaseManagerRow,
  makeComplianceSummary,
  makeHomeMeeting,
  makeRosterAttention,
  makeStaffHome,
} from '../test/fixtures';

vi.mock('@/features/district-admin/components/district-dashboard-tiles', () => ({
  DistrictDashboardTiles: () => <div data-testid="district-dashboard-tiles" />,
}));
vi.mock('@/features/district-admin/components/district-overview-card', () => ({
  DistrictOverviewCard: () => <div data-testid="district-overview-card" />,
}));
vi.mock('@/features/district-admin/components/setup-checklist-card', () => ({
  SetupChecklistCard: () => <div data-testid="setup-checklist-card" />,
}));
vi.mock('./adoption-engagement-teaser', () => ({
  AdoptionEngagementTeaser: () => <div data-testid="adoption-engagement-teaser" />,
}));

import { AdminHome } from './admin-home';

function renderAdmin(overrides: Parameters<typeof makeStaffHome>[0], isDistrict: boolean) {
  return render(
    <MemoryRouter>
      <AdminHome
        staff={makeStaffHome(overrides)}
        isDistrict={isDistrict}
        generatedAt="2026-09-16T00:00:00.000Z"
      />
    </MemoryRouter>
  );
}

describe('AdminHome', () => {
  it('SchoolAdmin: shows the building-scoped week, no district-only widgets', () => {
    renderAdmin({ variant: 'SchoolAdmin', meetingsThisWeek: [makeHomeMeeting()] }, false);

    expect(screen.getByText('Meetings this week in my building')).toBeInTheDocument();
    expect(screen.getByText('Brief · coming soon')).toBeInTheDocument();
    expect(screen.queryByTestId('setup-checklist-card')).not.toBeInTheDocument();
    expect(screen.queryByTestId('district-overview-card')).not.toBeInTheDocument();
    expect(screen.queryByTestId('adoption-engagement-teaser')).not.toBeInTheDocument();
    expect(screen.getByTestId('district-dashboard-tiles')).toBeInTheDocument();
  });

  it('sorts the overdue/at-risk table by student, not by case manager', () => {
    renderAdmin(
      {
        variant: 'SchoolAdmin',
        overdueByCaseManager: [
          makeCaseManagerRow({ studentId: 2, studentName: 'Zoe Adder', caseManagerName: 'Amy Aardvark' }),
          makeCaseManagerRow({ studentId: 1, studentName: 'Ada Lovelace', caseManagerName: 'Zack Zebra' }),
        ],
      },
      false
    );

    const table = screen.getByTestId('home-overdue-by-case-manager-table');
    const rows = within(table).getAllByRole('row').slice(1); // drop header row
    expect(rows[0]).toHaveTextContent('Ada Lovelace');
    expect(rows[1]).toHaveTextContent('Zoe Adder');
  });

  it('renders roster attention tiles that link to the roster with the matching filter and an "as of" date', () => {
    renderAdmin({ variant: 'SchoolAdmin', rosterAttention: makeRosterAttention() }, false);

    const tile = screen.getByTestId('home-roster-attention-overdueAnnual');
    expect(tile.closest('a')).toHaveAttribute('href', '/educator/students?attention=OverdueAnnual');
    expect(tile).toHaveTextContent(/As of/);
  });

  it('shows unsigned finalized as an empty hint, never an error, when empty', () => {
    renderAdmin({ variant: 'SchoolAdmin', unsignedFinalized: [] }, false);
    expect(screen.getByTestId('home-unsigned-finalized-empty')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('DistrictAdmin: adds the setup checklist, overview, compliance summary, and adoption teaser', () => {
    renderAdmin(
      { variant: 'DistrictAdmin', complianceSummary: makeComplianceSummary() },
      true
    );

    expect(screen.getByTestId('setup-checklist-card')).toBeInTheDocument();
    expect(screen.getByTestId('district-overview-card')).toBeInTheDocument();
    expect(screen.getByTestId('adoption-engagement-teaser')).toBeInTheDocument();
    expect(screen.getByText('Meetings this week')).toBeInTheDocument();

    const complianceTile = screen.getByTestId('home-compliance-summary-overdueAnnual');
    expect(complianceTile.closest('a')).toHaveAttribute(
      'href',
      '/educator/students?attention=OverdueAnnual'
    );
    const boardLink = screen.getByTestId('home-compliance-summary-board-link');
    expect(boardLink.closest('a')).toHaveAttribute('href', '/educator/admin/compliance');
  });
});
