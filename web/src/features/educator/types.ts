// Mirrors api/IepAssistant.Api/DTOs/Educator/*.cs

// Org role IDs are stable, seeded server-side (OrgRoleIds): 1=DistrictAdmin,
// 2=SchoolAdmin, 3=Teacher.
export const ORG_ROLE = {
  DistrictAdmin: 1,
  SchoolAdmin: 2,
  Teacher: 3,
} as const;

export type OrgRoleId = (typeof ORG_ROLE)[keyof typeof ORG_ROLE];

export interface EducatorProfile {
  staffProfileId: number;
  userId: number;
  orgRoleId: number;
  orgRoleName: string;
  districtId: number;
  districtName: string;
  schoolId?: number | null;
  schoolName?: string | null;
  isActive: boolean;
  stateCode?: string | null;
  title?: string | null;
  credentials?: string | null;
}

export interface SchoolStudent {
  id: number;
  schoolId: number;
  firstName: string;
  lastName?: string | null;
  dateOfBirth?: string | null;
  stateCode?: string | null;
  gradeLevel?: string | null;
  disabilityCategory?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface CreateSchoolStudentRequest {
  firstName: string;
  lastName?: string;
  dateOfBirth?: string;
  stateCode?: string;
  gradeLevel?: string;
  disabilityCategory?: string;
}

export interface InviteParentRequest {
  parentEmail: string;
}

export interface ChildLink {
  id: number;
  schoolStudentId: number;
  childProfileId?: number | null;
  inviteEmail?: string | null;
  isActive: boolean;
  isAccepted: boolean;
  acceptedAt?: string | null;
  linkedAt?: string | null;
  inviteExpiresAt?: string | null;
  createdAt: string;
}
