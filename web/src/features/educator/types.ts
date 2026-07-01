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
  schoolName?: string | null;
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
  // REQUIRED when the caller is a DistrictAdmin; omitted for SchoolAdmin/Teacher.
  schoolId?: number;
}

// Per-student access roles (mirrors AccessRole on the backend), serialized as
// their string name in the grant/list DTOs.
export type AccessRole = 'Viewer' | 'Collaborator' | 'Owner';

export const ACCESS_ROLES: AccessRole[] = ['Viewer', 'Collaborator', 'Owner'];

// An active staff↔student access grant — mirrors StudentStaffAccessDto.
export interface StudentStaffAccess {
  accessId: number;
  staffProfileId: number;
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  orgRoleName: string;
  accessRole: AccessRole;
  grantedAt: string;
}

export interface GrantStudentStaffAccessRequest {
  staffProfileId: number;
  accessRole?: AccessRole;
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
