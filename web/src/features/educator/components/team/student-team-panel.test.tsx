import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { apiRejection } from '@/test/axios-rejection';
import { ORG_ROLE } from '../../types';
import type { EligibleStaff } from '../../types';
import { makeMember } from '../../test/fixtures';

const api = vi.hoisted(() => ({
  getTeam: vi.fn(),
  getEligibleTeamStaff: vi.fn(),
  addTeamMember: vi.fn(),
  updateTeamMember: vi.fn(),
  setTeamLead: vi.fn(),
  removeTeamMember: vi.fn(),
}));
vi.mock('../../api/educator-api', () => api);

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

const eligible: EligibleStaff[] = [
  {
    staffProfileId: 72, userId: 9, firstName: 'Gena', lastName: 'Ed', email: 'gena@district.org',
    orgRoleId: ORG_ROLE.GeneralEducator, orgRoleName: 'GeneralEducator', schoolId: 5,
    schoolName: 'Lincoln Elementary',
  },
  {
    staffProfileId: 74, userId: 11, firstName: 'Ria', lastName: 'Roaming', email: 'ria@district.org',
    orgRoleId: ORG_ROLE.RelatedServiceProvider, orgRoleName: 'RelatedServiceProvider', schoolId: 6,
    schoolName: 'Other School',
  },
];

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => (resolve = r));
  return { promise, resolve };
}

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
    api.getEligibleTeamStaff.mockResolvedValue({ success: true, data: eligible });
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

  it('promotes a member via "Make lead" by adopting the returned DTO (no refetch)', async () => {
    const user = userEvent.setup();
    api.setTeamLead.mockResolvedValue({ success: true, data: { ...slp, isLead: true } });
    renderPanel();
    await screen.findByText('Sam Speech');

    await user.click(screen.getByRole('button', { name: 'Actions for Sam Speech' }));
    await user.click(screen.getByTestId('team-make-lead-101'));
    await waitFor(() => expect(api.setTeamLead).toHaveBeenCalledWith(10, 101));

    // Sam is now first and the only lead; Casey stays on the team demoted.
    await waitFor(() => {
      const [, first, second] = screen.getAllByRole('row');
      expect(within(first).getByText('Sam Speech')).toBeInTheDocument();
      expect(within(first).getByTestId('team-lead-badge')).toBeInTheDocument();
      expect(within(second).getByText('Casey Manager')).toBeInTheDocument();
      expect(within(second).queryByTestId('team-lead-badge')).not.toBeInTheDocument();
    });
    expect(api.getTeam).toHaveBeenCalledTimes(1);
  });

  it('surfaces the server refusal (4xx envelope) when removing the current lead', async () => {
    const user = userEvent.setup();
    api.removeTeamMember.mockRejectedValue(apiRejection('Choose a new lead case manager first.'));
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
    expect(screen.getByText('Casey Manager')).toBeInTheDocument();
  });

  it('removes a member when the API answers with an empty body and refreshes the picker', async () => {
    const user = userEvent.setup();
    // The api layer normalises a 204 to this shape; the panel must accept it.
    api.removeTeamMember.mockResolvedValue({ success: true, data: null });
    renderPanel();
    await screen.findByText('Sam Speech');
    await waitFor(() => expect(api.getEligibleTeamStaff).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole('button', { name: 'Actions for Sam Speech' }));
    await user.click(screen.getByTestId('team-remove-101'));
    await user.click(screen.getByTestId('team-remove-dialog-confirm'));

    await waitFor(() => expect(screen.queryByText('Sam Speech')).not.toBeInTheDocument());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    // Sam is eligible again, so the directory is refetched.
    await waitFor(() => expect(api.getEligibleTeamStaff).toHaveBeenCalledTimes(2));
  });

  it('changes a team role inline, holding the chosen value while saving', async () => {
    const user = userEvent.setup();
    const put = deferred<{ success: boolean; data: typeof slp }>();
    api.updateTeamMember.mockReturnValue(put.promise);
    renderPanel();
    await screen.findByText('Sam Speech');
    const select = screen.getByLabelText('Team role for Sam Speech');
    await user.selectOptions(select, 'Counselor');
    await waitFor(() =>
      expect(api.updateTeamMember).toHaveBeenCalledWith(10, 101, { teamRole: 'Counselor' })
    );
    // Neither disabled nor snapped back mid-save; focus stays put.
    expect(select).toHaveValue('Counselor');
    expect(select).toHaveAttribute('aria-busy', 'true');
    expect(select).not.toBeDisabled();
    expect(select).toHaveFocus();

    put.resolve({ success: true, data: { ...slp, teamRole: 'Counselor' } });
    await waitFor(() => expect(select).not.toHaveAttribute('aria-busy'));
    expect(select).toHaveValue('Counselor');
    expect(api.getTeam).toHaveBeenCalledTimes(1);
  });

  it('reverts the select and toasts the server message when a role change is refused', async () => {
    const user = userEvent.setup();
    api.updateTeamMember.mockRejectedValue(apiRejection('Only the lead may be the case manager.'));
    renderPanel();
    await screen.findByText('Sam Speech');
    const select = screen.getByLabelText('Team role for Sam Speech');
    await user.selectOptions(select, 'CaseManager');
    expect(await screen.findByText('Only the lead may be the case manager.')).toBeInTheDocument();
    await waitFor(() => expect(select).toHaveValue('SpeechLanguagePathologist'));
  });

  it('adds a member from the server-filtered eligible list, sending role + override', async () => {
    const user = userEvent.setup();
    api.addTeamMember.mockResolvedValue({
      success: true,
      data: makeMember({ id: 102, userId: 9, staffProfileId: 72, firstName: 'Gena', lastName: 'Ed', isLead: false, teamRole: 'GeneralEducationTeacher', accessRole: 'Viewer' }),
    });
    renderPanel();
    await screen.findByText('Sam Speech');

    const picker = await screen.findByLabelText('Staff member *');
    await waitFor(() => expect(within(picker).getAllByRole('option')).toHaveLength(3));
    expect(api.getEligibleTeamStaff).toHaveBeenCalledWith(10);

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
    // Adopted into the list without a refetch; gone from the picker.
    expect(await screen.findByText('Gena Ed')).toBeInTheDocument();
    expect(api.getTeam).toHaveBeenCalledTimes(1);
    await waitFor(() =>
      expect(within(screen.getByLabelText('Staff member *')).queryByRole('option', { name: /Gena Ed/ })).not.toBeInTheDocument()
    );
  });

  it('clears a hidden selection when the search filters the chosen person out', async () => {
    const user = userEvent.setup();
    renderPanel();
    const picker = await screen.findByLabelText('Staff member *');
    await waitFor(() => expect(within(picker).getAllByRole('option')).toHaveLength(3));
    await user.selectOptions(picker, '72');
    expect(picker).toHaveValue('72');

    await user.type(screen.getByLabelText('Find staff'), 'ria');
    expect(picker).toHaveValue('');
    await user.click(screen.getByTestId('team-add-submit'));
    expect(await screen.findByRole('alert')).toHaveTextContent('Select a staff member to add');
    expect(api.addTeamMember).not.toHaveBeenCalled();
  });

  it('shows a loading state while the directory is pending (never the "already on the team" copy)', async () => {
    const directory = deferred<{ success: boolean; data: EligibleStaff[] }>();
    api.getEligibleTeamStaff.mockReturnValue(directory.promise);
    renderPanel();
    await screen.findByText('Sam Speech');
    expect(screen.getByTestId('team-add-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('team-add-empty')).not.toBeInTheDocument();

    directory.resolve({ success: true, data: [] });
    expect(await screen.findByTestId('team-add-empty')).toBeInTheDocument();
  });

  it('says the directory is unavailable when the eligible request is refused', async () => {
    api.getEligibleTeamStaff.mockRejectedValue(apiRejection('Forbidden', 403));
    renderPanel();
    expect(await screen.findByTestId('team-add-unavailable')).toHaveTextContent('Staff directory unavailable');
    expect(screen.queryByTestId('team-add-empty')).not.toBeInTheDocument();
  });

  it('refetches the team when the student changes school (transfer)', async () => {
    const { rerender } = renderPanel();
    await screen.findByText('Sam Speech');
    expect(api.getTeam).toHaveBeenCalledTimes(1);

    api.getTeam.mockResolvedValue({ success: true, data: [lead] });
    rerender(
      <ToastProvider>
        <MemoryRouter>
          <StudentTeamPanel studentId={10} studentSchoolId={6} isAdmin currentUserId={99} />
        </MemoryRouter>
      </ToastProvider>
    );
    await waitFor(() => expect(api.getTeam).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByText('Sam Speech')).not.toBeInTheDocument());
    expect(screen.getByText('Casey Manager')).toBeInTheDocument();
  });

  it('lets only the newest team GET write the list', async () => {
    const first = deferred<{ success: boolean; data: (typeof lead)[] }>();
    const second = deferred<{ success: boolean; data: (typeof lead)[] }>();
    api.getTeam.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise);
    const { rerender } = renderPanel();
    rerender(
      <ToastProvider>
        <MemoryRouter>
          <StudentTeamPanel studentId={10} studentSchoolId={6} isAdmin currentUserId={99} />
        </MemoryRouter>
      </ToastProvider>
    );
    await waitFor(() => expect(api.getTeam).toHaveBeenCalledTimes(2));

    second.resolve({ success: true, data: [lead] });
    await screen.findByText('Casey Manager');
    first.resolve({ success: true, data: [lead, slp] });
    // The stale (older) response must not resurrect Sam.
    await new Promise((r) => setTimeout(r, 0));
    expect(screen.queryByText('Sam Speech')).not.toBeInTheDocument();
  });

  it('is read-only for a non-admin who is not the lead, but manageable for the lead', async () => {
    const { unmount } = renderPanel({ isAdmin: false, currentUserId: 8 });
    await screen.findByText('Casey Manager');
    expect(screen.getByTestId('team-role-100')).toHaveTextContent('Case manager');
    expect(screen.queryByRole('button', { name: /Actions for/ })).not.toBeInTheDocument();
    expect(screen.queryByTestId('team-add-form')).not.toBeInTheDocument();
    expect(api.getEligibleTeamStaff).not.toHaveBeenCalled();
    unmount();

    renderPanel({ isAdmin: false, currentUserId: 7 });
    expect(await screen.findByLabelText('Team role for Casey Manager')).toBeInTheDocument();
    expect(await screen.findByTestId('team-add-form')).toBeInTheDocument();
    expect(api.getEligibleTeamStaff).toHaveBeenCalledWith(10);
  });

  it('reports the loaded members to the host', async () => {
    const onMembersChange = vi.fn();
    renderPanel({ onMembersChange });
    await waitFor(() => expect(onMembersChange).toHaveBeenCalledWith([lead, slp]));
  });
});
