import '@testing-library/jest-dom';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
// `document-authoring` and `educator` are both staff-only namespaces (plan
// phase 5) — see `@/app/lazy-routes/staff-locales`'s doc comment and
// `docs/i18n/README.md`'s "Staff and admin namespaces". This component
// renders directly here (not through the lazy route), so their English must
// be registered the same way the real route chunk does. `educator` is
// needed because `TeamMemberSelect` now names it in its own
// `useTranslation` call (for `teamRoleLabel`).
import '@/app/lazy-routes/staff-locales';
import { TeamMemberSelect } from './team-member-select';
import type { StudentTeamCache } from '../hooks/use-student-team';

function teamOf(...members: StudentTeamCache['members']): StudentTeamCache {
  return { members, isLoading: false, isError: false };
}

const ana = {
  id: 1,
  userId: 7,
  staffProfileId: 1,
  firstName: 'Ana',
  lastName: 'Ito',
  email: 'ana@example.com',
  orgRoleName: 'RelatedServiceProvider',
  teamRole: 'OccupationalTherapist',
  isLead: false,
  accessRole: 'Collaborator',
  isActive: true,
  addedAt: '2026-01-01T00:00:00Z',
} as const;

const steph = {
  ...ana,
  id: 2,
  userId: 9,
  firstName: 'Steph',
  lastName: 'Case',
  teamRole: 'CaseManager',
} as const;

const removedMember = { ...ana, id: 3, userId: 11, firstName: 'Former', lastName: 'Member', isActive: false } as const;

describe('TeamMemberSelect', () => {
  it('lists active team members by name + role, with an Unassigned option', () => {
    render(<TeamMemberSelect team={teamOf(ana, steph)} value={undefined} onChange={() => {}} />);
    expect(screen.getByRole('option', { name: 'Unassigned' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Ana Ito — Occupational therapist' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Steph Case — Case manager' })).toBeInTheDocument();
  });

  it('shows the amber "No owner yet" hint when unset, and not when an owner is set', () => {
    const { rerender } = render(<TeamMemberSelect team={teamOf(ana)} value={undefined} onChange={() => {}} />);
    expect(screen.getByText('No owner yet')).toBeInTheDocument();

    rerender(<TeamMemberSelect team={teamOf(ana)} value={7} onChange={() => {}} />);
    expect(screen.queryByText('No owner yet')).not.toBeInTheDocument();
  });

  it('calls onChange with the selected user id, or undefined for Unassigned', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<TeamMemberSelect team={teamOf(ana, steph)} value={undefined} onChange={onChange} data-testid="owner-select" />);

    await user.selectOptions(screen.getByTestId('owner-select'), '9');
    expect(onChange).toHaveBeenLastCalledWith(9);

    await user.selectOptions(screen.getByTestId('owner-select'), 'Unassigned');
    expect(onChange).toHaveBeenLastCalledWith(undefined);
  });

  it('keeps a stale owner visible as "Former team member" instead of silently blanking the control', () => {
    render(<TeamMemberSelect team={teamOf(ana, removedMember)} value={11} onChange={() => {}} data-testid="owner-select" />);
    const select = screen.getByTestId('owner-select') as HTMLSelectElement;
    expect(select.value).toBe('11');
    expect(screen.getByRole('option', { name: 'Former team member' })).toBeInTheDocument();
    // "No owner yet" is only for an actually-unset owner.
    expect(screen.queryByText('No owner yet')).not.toBeInTheDocument();
  });

  it('shows a server save warning next to the control', () => {
    render(<TeamMemberSelect team={teamOf(ana)} value={7} onChange={() => {}} warning="Not an active team member." />);
    expect(screen.getByRole('alert')).toHaveTextContent('Not an active team member.');
  });

  it('disables the control while the team is still loading', () => {
    render(
      <TeamMemberSelect
        team={{ members: [], isLoading: true, isError: false }}
        value={undefined}
        onChange={() => {}}
        data-testid="owner-select"
      />
    );
    expect(screen.getByTestId('owner-select')).toBeDisabled();
  });

  it('shows "Owner unavailable" with a retry (not "Former team member") when the team failed to load', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    render(
      <TeamMemberSelect
        team={{ members: [], isLoading: false, isError: true, retry }}
        value={7}
        onChange={() => {}}
        data-testid="owner-select"
      />
    );

    expect(screen.getByTestId('owner-select')).toBeDisabled();
    expect(screen.getByRole('option', { name: 'Owner unavailable' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Former team member' })).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Owner unavailable');

    await user.click(screen.getByRole('button', { name: 'Retry' }));
    expect(retry).toHaveBeenCalledTimes(1);
  });

  it('does not show the amber "No owner yet" hint for an unset owner while the team is erroring', () => {
    render(
      <TeamMemberSelect
        team={{ members: [], isLoading: false, isError: true }}
        value={undefined}
        onChange={() => {}}
      />
    );
    expect(screen.queryByText('No owner yet')).not.toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent('Owner unavailable');
  });
});
