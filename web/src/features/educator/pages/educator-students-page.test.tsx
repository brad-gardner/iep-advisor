import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Link, MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
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
  return (
    <>
      <output data-testid="location">{location.search}</output>
      {/* Stands in for the sidebar "Students" link: same route, no query. */}
      <Link to="/educator/students">Reset link</Link>
      {/* Stands in for Back to a history entry that carries a previously typed q. */}
      <Link to="/educator/students?q=ab">Back to ab</Link>
    </>
  );
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

  it('keeps a filter changed inside the search debounce window', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Ada Lovelace');

    await user.type(screen.getByLabelText('Search students'), 'ab');
    await user.selectOptions(screen.getByLabelText('Status'), 'Exited');
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(
        expect.objectContaining({ query: 'ab', status: 'Exited' })
      )
    );
    expect(screen.getByTestId('location')).toHaveTextContent('status=Exited');
    expect(screen.getByTestId('location')).toHaveTextContent('q=ab');
    expect(screen.getByLabelText('Status')).toHaveValue('Exited');
  });

  it('adopts an external URL q change without clobbering typed text', async () => {
    const user = userEvent.setup();
    renderPage('/educator/students?q=ada');
    await screen.findByText('Ada Lovelace');
    const box = screen.getByLabelText('Search students');
    expect(box).toHaveValue('ada');

    // Sidebar-style navigation clears q while the page stays mounted.
    await user.click(screen.getByRole('link', { name: 'Reset link' }));
    await waitFor(() => expect(box).toHaveValue(''));

    // Typing wins over the URL catching up with what was typed.
    await user.type(box, 'al');
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('q=al'));
    expect(box).toHaveValue('al');
  });

  it('re-adopts a URL q it had typed earlier instead of debouncing it away', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Ada Lovelace');
    const box = screen.getByLabelText('Search students');

    await user.type(box, 'ab');
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('q=ab'));
    await user.click(screen.getByRole('link', { name: 'Reset link' }));
    await waitFor(() => expect(box).toHaveValue(''));

    await user.click(screen.getByRole('link', { name: 'Back to ab' }));
    await waitFor(() => expect(box).toHaveValue('ab'));
    // The debounce must not strip q again: the URL still carries it after the window.
    await new Promise((r) => setTimeout(r, 400));
    expect(screen.getByTestId('location')).toHaveTextContent('q=ab');
    expect(box).toHaveValue('ab');
  });

  it('resets to page 1 when the page size changes', async () => {
    const user = userEvent.setup();
    renderPage('/educator/students?page=3');
    await screen.findByText('Ada Lovelace');
    expect(api.searchStudents).toHaveBeenLastCalledWith(expect.objectContaining({ page: 3 }));

    await user.selectOptions(screen.getByLabelText('Rows per page'), '100');
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(
        expect.objectContaining({ page: 1, pageSize: 100 })
      )
    );
    expect(screen.getByTestId('location')).not.toHaveTextContent('page=');
  });

  it('keeps the ?attention= deep link: server-side narrowing with normal paging and Clear', async () => {
    const user = userEvent.setup();
    renderPage('/educator/students?attention=no-staff');
    expect(await screen.findByTestId('attention-filter-indicator')).toHaveTextContent('no case manager');
    expect(api.searchStudents).toHaveBeenCalledWith(
      expect.objectContaining({ attention: 'NoCaseManager', page: 1, pageSize: 50 })
    );
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(districtApi.getDistrictDashboard).not.toHaveBeenCalled();

    // Paging composes with the attention filter.
    await user.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(
        expect.objectContaining({ attention: 'NoCaseManager', page: 2 })
      )
    );

    await user.click(screen.getByTestId('attention-filter-clear'));
    await waitFor(() =>
      expect(api.searchStudents).toHaveBeenLastCalledWith(
        expect.objectContaining({ attention: undefined, page: 1 })
      )
    );
    expect(screen.queryByTestId('attention-filter-indicator')).not.toBeInTheDocument();
  });

  it('shows the roster as loading until the request resolves', async () => {
    let resolve!: (value: unknown) => void;
    api.searchStudents.mockReturnValue(new Promise((r) => (resolve = r)));
    renderPage('/educator/students?attention=no-parent');
    expect(screen.getByTestId('attention-filter-indicator')).toHaveTextContent('no linked parent');
    // Skeleton rows, not an empty state or an unfiltered list, while pending.
    expect(screen.queryByTestId('student-list-empty')).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: /Ada Lovelace/ })).not.toBeInTheDocument();

    resolve({ success: true, data: { items: students, total: 2, page: 1, pageSize: 50 } });
    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: /Ada Lovelace/ })).toBeInTheDocument();
  });

  it('announces the selection count from a persistent live region', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Ada Lovelace');
    const status = screen.getByTestId('roster-selection-status');
    expect(status).toHaveAttribute('aria-live', 'polite');
    expect(status).toHaveTextContent('');
    await user.click(screen.getByRole('checkbox', { name: /Ada Lovelace/ }));
    expect(status).toHaveTextContent('1 selected');
  });

  it('surfaces the server refusal when bulk assignment is rejected', async () => {
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
        ],
        pendingInvites: [],
      },
    });
    api.assignCaseManagerBulk.mockRejectedValue(
      apiRejection('Casey Manager is not allowed at Lincoln Elementary.')
    );
    renderPage();
    await screen.findByText('Ada Lovelace');
    await user.click(screen.getByRole('checkbox', { name: 'Select all rows on this page' }));
    await user.click(screen.getByTestId('roster-bulk-assign-case-manager'));
    const picker = await screen.findByLabelText('Case manager *');
    await waitFor(() => expect(picker).not.toBeDisabled());
    await user.selectOptions(picker, '7');
    await user.click(screen.getByTestId('roster-assign-case-manager-submit'));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Casey Manager is not allowed at Lincoln Elementary.'
    );
    expect(screen.getByTestId('roster-bulk-bar')).toBeInTheDocument();
  });
});
