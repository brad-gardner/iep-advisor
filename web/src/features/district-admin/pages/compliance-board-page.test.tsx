import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, RouterProvider, createMemoryRouter } from 'react-router-dom';
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
      dueInRange: 6,
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
        dueInRange: 3,
      },
    ],
    drill: {
      overdueAnnual: 'attention=OverdueAnnual',
      overdueReeval: 'attention=OverdueReeval',
      due30: 'attention=Due30',
      due60: 'attention=Due60',
      unknownDates: 'attention=UnknownDates',
      noLead: 'attention=NoCaseManager',
      dueInRange: 'attention=DueInRange&from=2026-09-16&to=2026-11-15',
    },
    ...overrides,
  };
}

function renderPage(initialPath = '/educator/admin/compliance') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <ComplianceBoardPage />
    </MemoryRouter>
  );
}

// A router with history the test can navigate directly, so back/forward can
// be exercised for real rather than only inferred from URL-parsing.
function renderWithHistory(initialPath: string) {
  const router = createMemoryRouter(
    [
      { path: '/other', element: <div data-testid="other-page">Other page</div> },
      { path: '/educator/admin/compliance', element: <ComplianceBoardPage /> },
    ],
    { initialEntries: ['/other', initialPath], initialIndex: 1 }
  );
  return { router, ...render(<RouterProvider router={router} />) };
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

  it('announces the load-failure notice to assistive tech via role=alert', async () => {
    districtApi.getComplianceBoard.mockResolvedValue({ success: false, message: 'Boom' });
    renderPage();
    expect(await screen.findByRole('alert')).toHaveTextContent('Boom');
  });

  it('renders a dueInRange tile and school-table column driven by the server drill map', async () => {
    renderPage();
    const tile = await screen.findByTestId('compliance-summary-dueInRange');
    expect(tile).toHaveTextContent('6');
    expect(tile.closest('a')).toHaveAttribute(
      'href',
      '/educator/students?attention=DueInRange&from=2026-09-16&to=2026-11-15'
    );

    const cell = screen.getByTestId('compliance-school-5-dueInRange');
    expect(cell).toHaveTextContent('3');
    expect(cell).toHaveAttribute(
      'href',
      '/educator/students?attention=DueInRange&from=2026-09-16&to=2026-11-15&school=5'
    );
  });

  it('restores school/range filters from the URL on a fresh load (reload or a shared link)', async () => {
    renderPage('/educator/admin/compliance?school=5&range=30');

    await waitFor(() =>
      expect(districtApi.getComplianceBoard).toHaveBeenCalledWith(expect.objectContaining({ schoolId: 5 }))
    );
    const call = districtApi.getComplianceBoard.mock.calls[0][0];
    const days = Math.round(
      (new Date(call.to).getTime() - new Date(call.from).getTime()) / (24 * 60 * 60 * 1000)
    );
    expect(days).toBe(30);
    expect(screen.getByLabelText('Filter by school')).toHaveValue('5');
    expect(screen.getByTestId('compliance-filter-range-30')).toHaveAttribute('aria-pressed', 'true');
  });

  it('reflects filter changes in the URL and survives navigating away and back', async () => {
    const user = userEvent.setup();
    const { router } = renderWithHistory('/educator/admin/compliance');
    await screen.findByTestId('compliance-summary-tiles');

    await user.selectOptions(screen.getByLabelText('Filter by school'), '5');
    await waitFor(() => expect(router.state.location.search).toContain('school=5'));

    await user.click(screen.getByTestId('compliance-filter-range-30'));
    await waitFor(() => expect(router.state.location.search).toContain('range=30'));
    expect(router.state.location.search).toContain('school=5');

    await act(async () => {
      await router.navigate('/other');
    });
    await screen.findByTestId('other-page');

    await act(async () => {
      await router.navigate(-1);
    });
    await screen.findByTestId('compliance-summary-tiles');
    expect(router.state.location.search).toContain('school=5');
    expect(router.state.location.search).toContain('range=30');
    expect(screen.getByLabelText('Filter by school')).toHaveValue('5');
    expect(screen.getByTestId('compliance-filter-range-30')).toHaveAttribute('aria-pressed', 'true');
  });

  it('fetches adoption/engagement in parallel with the board on a filter change, never gated on the board resolving', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByTestId('compliance-summary-tiles');
    await screen.findByTestId('adoption-engagement-tiles');

    districtApi.getAdoption.mockClear();
    districtApi.getEngagement.mockClear();
    let resolveBoard!: (value: { success: boolean; data: ComplianceBoardDto }) => void;
    districtApi.getComplianceBoard.mockReturnValue(
      new Promise((resolve) => {
        resolveBoard = resolve;
      })
    );

    await user.selectOptions(screen.getByLabelText('Filter by school'), '5');

    // The board fetch is still pending for the new school...
    expect(screen.getByTestId('compliance-board-loading')).toBeInTheDocument();
    // ...but adoption/engagement already fired for it, independent of the board.
    await waitFor(() =>
      expect(districtApi.getAdoption).toHaveBeenCalledWith(expect.objectContaining({ schoolId: 5 }))
    );
    expect(districtApi.getEngagement).toHaveBeenCalledWith(expect.objectContaining({ schoolId: 5 }));

    resolveBoard({ success: true, data: makeBoard() });
    await screen.findByTestId('compliance-summary-tiles');
  });
});
