import { TEAM_ROLE_LABELS } from '@/features/educator/types';
import { teamMemberName } from '@/features/educator/components/team/team-eligibility';
import type { StudentTeamCache } from '../hooks/use-student-team';

export interface OwnerDisplay {
  /** "Jordan Ellis — Intervention specialist", "Former team member", or "Loading…". */
  label: string;
  /** True when the id no longer matches an active team member. */
  former: boolean;
  /** True while the team cache hasn't finished its first load yet — the label is a
   *  placeholder, not a verdict, so callers can style it more quietly. */
  loading: boolean;
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
  if (!team || team.isLoading) return { label: 'Loading…', former: false, loading: true };

  const member = team.members.find((m) => m.userId === ownerUserId && m.isActive);
  if (member) {
    return { label: `${teamMemberName(member)} — ${TEAM_ROLE_LABELS[member.teamRole]}`, former: false, loading: false };
  }
  return { label: 'Former team member', former: true, loading: false };
}
