import { TEAM_ROLE_LABELS } from '@/features/educator/types';
import { teamMemberName } from '@/features/educator/components/team/team-eligibility';
import type { StudentTeamCache } from '../hooks/use-student-team';

export interface OwnerDisplay {
  /** "Jordan Ellis — Intervention specialist", "Former team member", "Owner
   *  unavailable", or "Loading…". */
  label: string;
  /** True when the id no longer matches an active team member. */
  former: boolean;
  /** True while the team cache hasn't finished its first load yet — the label is a
   *  placeholder, not a verdict, so callers can style it more quietly. */
  loading: boolean;
  /** True when the team failed to load — the id's status genuinely can't be
   *  determined (it may well still be an active owner), so this is distinct
   *  from `former`, which asserts the id is known NOT to be active. */
  error: boolean;
}

/**
 * Resolves an `_ownerUserId` to a display label for the EDUCATOR read views (name +
 * role — never used for family/student-facing output, which gets role-only text
 * from the server's `_ownerRole`). Returns null when the row has no owner at all.
 */
export function resolveOwnerDisplay(
  ownerUserId: number | undefined,
  team: StudentTeamCache | undefined
): OwnerDisplay | null {
  if (ownerUserId == null) return null;
  if (!team || team.isLoading) return { label: 'Loading…', former: false, loading: true, error: false };
  // A failed fetch leaves `members` empty, which would otherwise read as "no
  // longer an active member" — an unrelated claim this id's owner status was
  // never actually checked against.
  if (team.isError) return { label: 'Owner unavailable', former: false, loading: false, error: true };

  const member = team.members.find((m) => m.userId === ownerUserId && m.isActive);
  if (member) {
    return { label: `${teamMemberName(member)} — ${TEAM_ROLE_LABELS[member.teamRole]}`, former: false, loading: false, error: false };
  }
  return { label: 'Former team member', former: true, loading: false, error: false };
}
