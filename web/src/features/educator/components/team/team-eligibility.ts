import type { EligibleStaff, StudentTeamMember } from '../../types';

export function teamMemberName(member: StudentTeamMember): string {
  return `${member.firstName} ${member.lastName}`.trim() || member.email;
}

// Eligibility (same school or a district-wide provider, active, non-admin) is
// decided by the server (`GET .../team/eligible`); this only hides anyone who
// joined the team since the directory was fetched.
export function eligibleTeamStaff(
  staff: EligibleStaff[],
  memberProfileIds: ReadonlySet<number>
): EligibleStaff[] {
  return staff.filter((m) => !memberProfileIds.has(m.staffProfileId));
}
