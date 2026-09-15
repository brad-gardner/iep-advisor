import type { StaffMember } from '@/features/staff-invites/types';
import { ORG_ROLE } from '../../types';
import type { StudentTeamMember } from '../../types';

export function teamMemberName(member: StudentTeamMember): string {
  return `${member.firstName} ${member.lastName}`.trim() || member.email;
}

// Team members must be active staff at the student's school, or a related
// service provider anywhere in the district; DistrictAdmins act by scope and
// are never members. Mirrors the server rule so the picker never offers a
// row the API would reject.
export function eligibleTeamStaff(
  staff: StaffMember[],
  studentSchoolId: number,
  memberProfileIds: ReadonlySet<number>
): StaffMember[] {
  return staff.filter(
    (m) =>
      m.isActive &&
      m.orgRoleId !== ORG_ROLE.DistrictAdmin &&
      !memberProfileIds.has(m.staffProfileId) &&
      (m.schoolId === studentSchoolId || m.orgRoleId === ORG_ROLE.RelatedServiceProvider)
  );
}
