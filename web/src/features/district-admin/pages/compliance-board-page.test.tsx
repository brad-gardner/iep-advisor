import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ORG_ROLE, type EducatorProfile } from '@/features/educator/types';
import type { ComplianceBoardDto } from '../types';

const useEducatorProfileMock = vi.fn();
vi.mock('@/features/educator/hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const districtApi = vi.hoisted(() => ({
  getComplianceBoard: vi.fn(),
  getDistrictSchools: vi.fn(),
  getAdoption: vi.fn(),
  getEngagement: vi.fn(),
}));
vi.mock('@/features/district-admin/api/district-api', () => districtApi);

import { ComplianceBoardPage } from './compliance-board-page';

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

function makeBoard(overrides: Partial<ComplianceBoardDto> = {}): ComplianceBoardDto {
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
    },
    bySchool: [
      {
        schoolId: 5,
        schoolName: 'Lincoln Elementary',
        activeStudents: 60,
        overdueAnnual: 2,
        overdueReeval: 1,
        due30: 3,
        due60: 4,
        unknownDates: 1,
        noLead: 2,
      },
    ],
    drill: {
      overdueAnnual: 'attention=OverdueAnnual',
      overdueReeval: 'attention=OverdueReeval',
      due30: 'attention=Due30',
      due60: 'attention=Due60',
      unknownDates: 'attention=UnknownDates',
      noLead: 'attention=NoCaseManager',
    },
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <ComplianceBoardPage />
    </MemoryRouter>
  );
}

describe('ComplianceBoardPage', () => {
  beforeEach(() => {
    Object.values(districtApi).forEach((fn) => fn.mockReset());
    districtApi.getComplianceBoard.mockResolvedValue({ success: true, data: makeBoard() });
    districtApi.getDistrictSchools.mockResolvedValue({
      success: true,
      data: [{ id: 5, name: 'Lincoln Elementary', activeStudentCount: 60, activeStaffCount: 10 }],
    });
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
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile() });
  });

  it('loads the default 60-day range and renders summary tiles linking to the roster via the drill map', async () => {
    renderPage();

    await waitFor(() =>
      expect(districtApi.getComplianceBoard).toHaveBeenCalledWith(
        expect.objectContaining({ schoolId: undefined })
      )
    );
    const firstCall = districtApi.getComplianceBoard.mock.calls[0][0];
    expect(firstCall.from).toBeTruthy();
    expect(firstCall.to).toBeTruthy();

    const tile = await screen.findByTestId('compliance-summary-overdueAnnual');
    expect(tile.closest('a')).toHaveAttribute('href', '/educator/students?attention=OverdueAnnual');
  });

  it('changing the date-range preset re-fetches with a new date range', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByTestId('compliance-summary-tiles');
    districtApi.getComplianceBoard.mockClear();

    await user.click(screen.getByTestId('compliance-filter-range-30'));

    await waitFor(() => expect(districtApi.getComplianceBoard).toHaveBeenCalledTimes(1));
    const call = districtApi.getComplianceBoard.mock.calls[0][0];
    const from = new Date(call.from);
    const to = new Date(call.to);
    const days = Math.round((to.getTime() - from.getTime()) / (24 * 60 * 60 * 1000));
    expect(days).toBe(30);
  });

  it('selecting a school scopes both the board request and the per-school drill links', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByTestId('compliance-summary-tiles');
    districtApi.getComplianceBoard.mockClear();

    await user.selectOptions(screen.getByLabelText('Filter by school'), '5');

    await waitFor(() =>
      expect(districtApi.getComplianceBoard).toHaveBeenCalledWith(expect.objectContaining({ schoolId: 5 }))
    );
    const tile = await screen.findByTestId('compliance-summary-overdueAnnual');
    expect(tile.closest('a')).toHaveAttribute(
      'href',
      '/educator/students?attention=OverdueAnnual&school=5'
    );
  });

  it('links each per-school numeric cell to the roster, scoped to that school', async () => {
    renderPage();
    const cell = await screen.findByTestId('compliance-school-5-overdueAnnual');
    expect(cell).toHaveAttribute('href', '/educator/students?attention=OverdueAnnual&school=5');
  });

  it('SchoolAdmin never sees a school picker and never sends a schoolId', async () => {
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ orgRoleId: ORG_ROLE.SchoolAdmin, orgRoleName: 'SchoolAdmin', schoolId: 5 }),
    });
    renderPage();

    await waitFor(() => expect(districtApi.getComplianceBoard).toHaveBeenCalled());
    expect(screen.queryByLabelText('Filter by school')).not.toBeInTheDocument();
    expect(districtApi.getComplianceBoard.mock.calls[0][0].schoolId).toBeUndefined();
  });

  it('shows an error notice with retry on load failure', async () => {
    const user = userEvent.setup();
    districtApi.getComplianceBoard.mockResolvedValue({ success: false, message: 'Boom' });
    renderPage();

    expect(await screen.findByText('Boom')).toBeInTheDocument();
    districtApi.getComplianceBoard.mockResolvedValue({ success: true, data: makeBoard() });
    await user.click(screen.getByTestId('compliance-board-retry'));
    await screen.findByTestId('compliance-summary-tiles');
  });
});
