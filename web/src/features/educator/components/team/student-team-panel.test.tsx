import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { ORG_ROLE } from '../../types';
import { makeMember } from '../../test/fixtures';

const api = vi.hoisted(() => ({
  getTeam: vi.fn(),
  addTeamMember: vi.fn(),
  updateTeamMember: vi.fn(),
  setTeamLead: vi.fn(),
  removeTeamMember: vi.fn(),
}));
vi.mock('../../api/educator-api', () => api);

const staffApi = vi.hoisted(() => ({ getStaffList: vi.fn() }));
vi.mock('@/features/staff-invites/api/staff-invites-api', () => staffApi);

import { StudentTeamPanel } from './student-team-panel';

const lead = makeMember();
const slp = makeMember({
  id: 101,
  userId: 8,
  staffProfileId: 71,
  firstName: 'Sam',
  lastName: 'Speech',
  email: 'sam@district.org',
  orgRoleName: 'RelatedServiceProvider',
  teamRole: 'SpeechLanguagePathologist',
  isLead: false,
  accessRole: 'Collaborator',
});

function renderPanel(props: Partial<React.ComponentProps<typeof StudentTeamPanel>> = {}) {
  return render(
    <ToastProvider>
      <MemoryRouter>
        <StudentTeamPanel
          studentId={10}
          studentSchoolId={5}
          isAdmin
          currentUserId={99}
          family={<p data-testid="family-slot">Family here</p>}
          {...props}
        />
      </MemoryRouter>
    </ToastProvider>
  );
}

describe('StudentTeamPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.getTeam.mockResolvedValue({ success: true, data: [lead, slp] });
    staffApi.getStaffList.mockResolvedValue({
      success: true,
      data: {
        members: [
          {
            staffProfileId: 72, userId: 9, firstName: 'Gena', lastName: 'Ed', email: 'gena@district.org',
            orgRoleId: ORG_ROLE.GeneralEducator, orgRoleName: 'GeneralEducator', schoolId: 5,
            schoolName: 'Lincoln Elementary', isActive: true,
          },
          {
            staffProfileId: 73, userId: 10, firstName: 'Otto', lastName: 'Other', email: 'otto@district.org',
            orgRoleId: ORG_ROLE.Teacher, orgRoleName: 'Teacher', schoolId: 6, schoolName: 'Other School',
            isActive: true,
          },
          {
            staffProfileId: 74, userId: 11, firstName: 'Ria', lastName: 'Roaming', email: 'ria@district.org',
            orgRoleId: ORG_ROLE.RelatedServiceProvider, orgRoleName: 'RelatedServiceProvider', schoolId: 6,
            schoolName: 'Other School', isActive: true,
          },
        ],
        pendingInvites: [],
      },
    });
  });

  it('shows the lead first with a lead badge, and permission badges distinct from the role', async () => {
    renderPanel();
    await screen.findByText('Casey Manager');
    const [, first, second] = screen.getAllByRole('row');
    expect(within(first).getByText('Casey Manager')).toBeInTheDocument();
    expect(within(first).getByTestId('team-lead-badge')).toHaveTextContent('Lead case manager');
    expect(within(second).queryByTestId('team-lead-badge')).not.toBeInTheDocument();

    // Role (select) and permission (badge) are separate controls.
    expect(within(first).getByLabelText('Team role for Casey Manager')).toHaveValue('CaseManager');
    expect(screen.getByTestId('team-permission-100')).toHaveTextContent('Owner');
    expect(screen.getByTestId('team-permission-101')).toHaveTextContent('Collaborator');
    expect(screen.getByTestId('family-slot')).toBeInTheDocument();
  });

  it('promotes a member via "Make lead"', async () => {
    const user = userEvent.setup();
    api.setTeamLead.mockResolvedValue({ success: true, data: { ...slp, isLead: true } });
    renderPanel();
    await screen.findByText('Sam Speech');

    await user.click(screen.getByRole('button', { name: 'Actions for Sam Speech' }));
    await user.click(screen.getByTestId('team-make-lead-101'));
    await waitFor(() => expect(api.setTeamLead).toHaveBeenCalledWith(10, 101));
    expect(api.getTeam).toHaveBeenCalledTimes(2);
  });

  it('surfaces the server refusal when removing the current lead', async () => {
    const user = userEvent.setup();
    api.removeTeamMember.mockResolvedValue({
      success: false,
      message: 'Choose a new lead case manager first.',
    });
    renderPanel();
    await screen.findByText('Casey Manager');

    await user.click(screen.getByRole('button', { name: 'Actions for Casey Manager' }));
    // The lead has no "Make lead" item, only Remove.
    expect(screen.queryByTestId('team-make-lead-100')).not.toBeInTheDocument();
    await user.click(screen.getByTestId('team-remove-100'));
    await user.click(screen.getByTestId('team-remove-dialog-confirm'));

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose a new lead case manager first.');
    expect(api.removeTeamMember).toHaveBeenCalledWith(10, 100);
    // Dialog stays open for the user to cancel.
    expect(screen.getByTestId('team-remove-dialog-cancel')).toBeInTheDocument();
  });

  it('changes a team role inline', async () => {
    const user = userEvent.setup();
    api.updateTeamMember.mockResolvedValue({ success: true, data: { ...slp, teamRole: 'Counselor' } });
    renderPanel();
    await screen.findByText('Sam Speech');
    await user.selectOptions(screen.getByLabelText('Team role for Sam Speech'), 'Counselor');
    await waitFor(() =>
      expect(api.updateTeamMember).toHaveBeenCalledWith(10, 101, { teamRole: 'Counselor' })
    );
  });

  it('adds a member: search filters eligible staff (own school or provider), sends role + override', async () => {
    const user = userEvent.setup();
    api.addTeamMember.mockResolvedValue({ success: true, data: makeMember({ id: 102, staffProfileId: 72 }) });
    renderPanel();
    await screen.findByText('Sam Speech');

    const picker = await screen.findByLabelText('Staff member *');
    // Gena (same school) + Ria (provider elsewhere) are offered; Otto (other school teacher) is not.
    await waitFor(() => expect(within(picker).getAllByRole('option')).toHaveLength(3));
    expect(within(picker).queryByRole('option', { name: /Otto Other/ })).not.toBeInTheDocument();

    await user.type(screen.getByLabelText('Find staff'), 'gena@');
    expect(within(picker).getAllByRole('option')).toHaveLength(2);
    await user.selectOptions(picker, '72');
    await user.selectOptions(screen.getByLabelText('Team role *'), 'GeneralEducationTeacher');
    await user.selectOptions(screen.getByLabelText('Permission'), 'Viewer');
    await user.click(screen.getByTestId('team-add-submit'));

    await waitFor(() =>
      expect(api.addTeamMember).toHaveBeenCalledWith(10, {
        staffProfileId: 72,
        teamRole: 'GeneralEducationTeacher',
        accessRole: 'Viewer',
      })
    );
  });

  it('is read-only for a non-admin who is not the lead, but manageable for the lead', async () => {
    const { unmount } = renderPanel({ isAdmin: false, currentUserId: 8 });
    await screen.findByText('Casey Manager');
    expect(screen.getByTestId('team-role-100')).toHaveTextContent('Case manager');
    expect(screen.queryByRole('button', { name: /Actions for/ })).not.toBeInTheDocument();
    expect(screen.queryByTestId('team-add-form')).not.toBeInTheDocument();
    unmount();

    renderPanel({ isAdmin: false, currentUserId: 7 });
    expect(await screen.findByLabelText('Team role for Casey Manager')).toBeInTheDocument();
    expect(await screen.findByTestId('team-add-form')).toBeInTheDocument();
  });
});
