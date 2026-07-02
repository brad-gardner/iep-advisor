// Mirrors api/IepAssistant.Api/DTOs/Staff/StaffDtos.cs

// A created/resent invite. InviteUrl is only present under the dev-gated
// Email:ExposeLinksForTesting condition — when present, show a copyable link.
export interface StaffInvite {
  id: number;
  email: string;
  orgRoleId: number;
  orgRoleName: string;
  schoolId?: number | null;
  schoolName?: string | null;
  inviteExpiresAt: string;
  inviteUrl?: string | null;
}

export interface StaffMember {
  staffProfileId: number;
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  orgRoleId: number;
  orgRoleName: string;
  schoolId?: number | null;
  schoolName?: string | null;
  isActive: boolean;
}

export type PendingInviteStatus = 'pending' | 'expired';

export interface StaffPendingInvite {
  id: number;
  email: string;
  orgRoleId: number;
  orgRoleName: string;
  schoolId?: number | null;
  schoolName?: string | null;
  inviteExpiresAt: string;
  status: PendingInviteStatus;
}

export interface StaffList {
  members: StaffMember[];
  pendingInvites: StaffPendingInvite[];
}

// Response to a staff deactivation — carries the reassignment hint (students the
// deactivated staff member solely owned among non-admin staff).
export interface DeactivatedStaffStudent {
  studentId: number;
  name: string;
}

export interface DeactivateStaffResponse {
  solelyOwnedStudentCount: number;
  solelyOwnedStudents: DeactivatedStaffStudent[];
}

export interface CreateStaffInviteRequest {
  email: string;
  orgRoleId: number;
  schoolId?: number;
}

export type StaffInvitePreviewStatus = 'valid' | 'expired' | 'invalid';

export interface StaffInvitePreview {
  status: StaffInvitePreviewStatus;
  email?: string;
  districtName?: string;
  schoolName?: string | null;
  roleName?: string;
}

export interface AcceptStaffInviteRequest {
  token: string;
  firstName: string;
  lastName: string;
  password: string;
}
