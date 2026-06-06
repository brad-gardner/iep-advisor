// Mirrors api/IepAssistant.Api/DTOs/Educator/*.cs

export interface OnboardEducatorRequest {
  districtName: string;
  schoolName: string;
  stateCode?: string;
}

export interface EducatorProfile {
  teacherProfileId: number;
  userId: number;
  schoolId: number;
  schoolName: string;
  districtId: number;
  districtName: string;
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
