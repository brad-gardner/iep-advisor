import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { ORG_ROLE } from '../types';
import { makeProfile, makeStudent } from '../test/fixtures';

const useEducatorProfileMock = vi.fn();
vi.mock('../hooks/use-educator-profile', () => ({
  useEducatorProfile: () => useEducatorProfileMock(),
}));

const api = vi.hoisted(() => ({
  searchStudents: vi.fn(),
  createStudent: vi.fn(),
  assignCaseManagerBulk: vi.fn(),
}));
vi.mock('../api/educator-api', () => api);

const districtApi = vi.hoisted(() => ({
  getDistrictSchools: vi.fn(),
  getDistrictDashboard: vi.fn(),
}));
vi.mock('@/features/district-admin/api/district-api', () => districtApi);

const staffApi = vi.hoisted(() => ({ getStaffList: vi.fn() }));
vi.mock('@/features/staff-invites/api/staff-invites-api', () => staffApi);

import { EducatorStudentsPage } from './educator-students-page';

function LocationProbe() {
  const location = useLocation();
  return <output data-testid="location">{location.search}</output>;
}

function renderPage(initialEntry = '/educator/students') {
  return render(
    <ToastProvider>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route
            path="/educator/students"
            element={
              <>
                <EducatorStudentsPage />
                <LocationProbe />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </ToastProvider>
  );
}

const students = [
  makeStudent({ id: 1, firstName: 'Ada', lastName: 'Lovelace', externalStudentId: '000123' }),
  makeStudent({ id: 2, firstName: 'Alan', lastName: 'Turing', externalStudentId: '000124', status: 'Exited' }),
];

describe('EducatorStudentsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useEducatorProfileMock.mockReturnValue({ profile: makeProfile(), isLoading: false });
    api.searchStudents.mockResolvedValue({
      success: true,
      data: { items: students, total: 120, page: 1, pageSize: 50 },
    });
    districtApi.getDistrictSchools.mockResolvedValue({
      success: true,
      data: [{ id: 5, name: 'Lincoln Elementary', activeStudentCount: 2, activeStaffCount: 1 }],
    });
    staffApi.getStaffList.mockResolvedValue({ success: true, data: { members: [], pendingInvites: [] } });
  });

  it('fetches the active roster by default and renders ID + status columns', async () => {
    renderPage();
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(api.searchStudents).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'Active', page: 1, pageSize: 50, query: undefined })
    );
    expect(screen.getByText('000123')).toBeInTheDocument();
    expect(screen.getByTestId('student-status-Exited')).toHaveTextContent('Exited');
    expect(screen.getByTestId('student-list-pagination-summary')).toHaveTextContent('Showing 1–50 of 120');
  });

  it('pushes status/grade filters and paging into the URL and refetches', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Ada Lovelace');

    await user.selectOptions(screen.getByLabelText('Status'), 'Exited');
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Exited' }))
    );
    expect(screen.getByTestId('location')).toHaveTextContent('status=Exited');

    await user.selectOptions(screen.getByLabelText('Grade'), 'G5');
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(expect.objectContaining({ grade: 'G5' }))
    );

    await user.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }))
    );
    expect(screen.getByTestId('location')).toHaveTextContent('page=2');

    // A filter change resets to page 1.
    await user.selectOptions(screen.getByLabelText('Status'), 'All');
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'All', page: 1 })
      )
    );
  });

  it('debounces the search box: one query for a burst of keystrokes', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Ada Lovelace');
    const initialCalls = api.searchStudents.mock.calls.length;

    await user.type(screen.getByLabelText('Search students'), 'ada');
    // Nothing fires synchronously on keystrokes.
    expect(api.searchStudents).toHaveBeenCalledTimes(initialCalls);

    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(expect.objectContaining({ query: 'ada' }))
    );
    expect(api.searchStudents).toHaveBeenCalledTimes(initialCalls + 1);
    expect(screen.getByTestId('location')).toHaveTextContent('q=ada');
  });

  it('lets an admin bulk-select rows and assign a case manager', async () => {
    const user = userEvent.setup();
    staffApi.getStaffList.mockResolvedValue({
      success: true,
      data: {
        members: [
          {
            staffProfileId: 70, userId: 7, firstName: 'Casey', lastName: 'Manager',
            email: 'casey@district.org', orgRoleId: ORG_ROLE.Teacher, orgRoleName: 'Teacher',
            schoolId: 5, schoolName: 'Lincoln Elementary', isActive: true,
          },
          {
            staffProfileId: 71, userId: 8, firstName: 'Dana', lastName: 'Admin',
            email: 'dana@district.org', orgRoleId: ORG_ROLE.DistrictAdmin, orgRoleName: 'DistrictAdmin',
            schoolId: null, schoolName: null, isActive: true,
          },
        ],
        pendingInvites: [],
      },
    });
    api.assignCaseManagerBulk.mockResolvedValue({ success: true, data: { updated: 2 } });
    renderPage();
    await screen.findByText('Ada Lovelace');

    expect(screen.queryByTestId('roster-bulk-bar')).not.toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', { name: 'Select all rows on this page' }));
    expect(screen.getByTestId('roster-bulk-bar')).toHaveTextContent('2 selected');

    await user.click(screen.getByTestId('roster-bulk-assign-case-manager'));
    const picker = await screen.findByLabelText('Case manager *');
    await waitFor(() => expect(picker).not.toBeDisabled());
    // DistrictAdmins are never team members, so Dana is not offered.
    expect(screen.queryByRole('option', { name: /Dana Admin/ })).not.toBeInTheDocument();
    await user.selectOptions(picker, '7');
    await user.click(screen.getByTestId('roster-assign-case-manager-submit'));

    await waitFor(() =>
      expect(api.assignCaseManagerBulk).toHaveBeenCalledWith({ studentIds: [1, 2], userId: 7 })
    );
    await waitFor(() => expect(screen.queryByTestId('roster-bulk-bar')).not.toBeInTheDocument());
  });

  it('hides selection, import and the school filter from caseload staff', async () => {
    useEducatorProfileMock.mockReturnValue({
      profile: makeProfile({ orgRoleId: ORG_ROLE.RelatedServiceProvider, orgRoleName: 'RelatedServiceProvider' }),
      isLoading: false,
    });
    renderPage();
    await screen.findByText('Ada Lovelace');
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(screen.queryByTestId('educator-students-import')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Filter by school')).not.toBeInTheDocument();
  });

  it('keeps the ?attention= deep link: whole roster fetched, filtered to dashboard IDs', async () => {
    districtApi.getDistrictDashboard.mockResolvedValue({
      success: true,
      data: {
        schools: [], staffSummary: { activeCount: 0, deactivatedCount: 0, invitedCount: 0 },
        invitesNeedingAttention: [],
        studentsWithoutStaff: [{ schoolStudentId: 2, firstName: 'Alan', lastName: 'Turing', schoolName: 'x' }],
        studentsWithoutParent: [],
      },
    });
    renderPage('/educator/students?attention=no-staff');
    expect(await screen.findByTestId('attention-filter-indicator')).toHaveTextContent('no case manager');
    expect(api.searchStudents).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 500 }));
    await waitFor(() => expect(screen.queryByText('Ada Lovelace')).not.toBeInTheDocument());
    expect(screen.getByText('Alan Turing')).toBeInTheDocument();
    expect(screen.queryByTestId('student-list-pagination')).not.toBeInTheDocument();
  });
});
