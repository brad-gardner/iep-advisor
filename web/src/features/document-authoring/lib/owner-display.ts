import i18n from '@/lib/i18n';
import { teamMemberName } from '@/features/educator/components/team/team-eligibility';
import { teamRoleLabel } from '@/features/educator/lib/student-enum-labels';
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
 *
 * A found member's role renders via `teamRoleLabel` (`educator:teamRole.*`,
 * staff-only — i18n plan phase 5): every caller of this function must name
 * `educator` in its own `useTranslation` call (alongside `document-authoring`),
 * same as `TeamMemberSelect`, or a language switch won't re-render once that
 * namespace's Spanish loads — see `docs/i18n/README.md`'s "Staff and admin
 * namespaces".
 */
export function resolveOwnerDisplay(
  ownerUserId: number | undefined,
  team: StudentTeamCache | undefined
): OwnerDisplay | null {
  if (ownerUserId == null) return null;
  if (!team || team.isLoading)
    return { label: i18n.t('document-authoring:ownerDisplay.loading'), former: false, loading: true, error: false };
  // A failed fetch leaves `members` empty, which would otherwise read as "no
  // longer an active member" — an unrelated claim this id's owner status was
  // never actually checked against.
  if (team.isError)
    return { label: i18n.t('document-authoring:ownerDisplay.ownerUnavailable'), former: false, loading: false, error: true };

  const member = team.members.find((m) => m.userId === ownerUserId && m.isActive);
  if (member) {
    return { label: `${teamMemberName(member)} — ${teamRoleLabel(member.teamRole)}`, former: false, loading: false, error: false };
  }
  return { label: i18n.t('document-authoring:ownerDisplay.formerTeamMember'), former: true, loading: false, error: false };
}
