import { describe, expect, it } from 'vitest';
import { resolveOwnerDisplay } from './owner-display';
import type { StudentTeamCache } from '../hooks/use-student-team';

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

const removed = { ...ana, id: 2, userId: 11, isActive: false } as const;

describe('resolveOwnerDisplay', () => {
  it('returns null when the row has no owner', () => {
    expect(resolveOwnerDisplay(undefined, { members: [ana], isLoading: false, isError: false })).toBeNull();
  });

  it('returns a loading placeholder while the team cache is still loading, even with no team yet', () => {
    expect(resolveOwnerDisplay(7, undefined)).toEqual({ label: 'Loading…', former: false, loading: true, error: false });
    expect(resolveOwnerDisplay(7, { members: [], isLoading: true, isError: false })).toEqual({
      label: 'Loading…',
      former: false,
      loading: true,
      error: false,
    });
  });

  it('resolves an active member to "Name — Role"', () => {
    expect(resolveOwnerDisplay(7, { members: [ana], isLoading: false, isError: false })).toEqual({
      label: 'Ana Ito — Occupational therapist',
      former: false,
      loading: false,
      error: false,
    });
  });

  it('labels an id that is no longer an active member "Former team member"', () => {
    expect(resolveOwnerDisplay(11, { members: [removed], isLoading: false, isError: false })).toEqual({
      label: 'Former team member',
      former: true,
      loading: false,
      error: false,
    });
  });

  it('labels an id "Owner unavailable" (not "Former team member") when the team failed to load', () => {
    const erroredTeam: StudentTeamCache = { members: [], isLoading: false, isError: true };
    expect(resolveOwnerDisplay(7, erroredTeam)).toEqual({
      label: 'Owner unavailable',
      former: false,
      loading: false,
      error: true,
    });
  });
});
