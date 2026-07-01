// Mirrors api/IepAssistant.Api/DTOs/District/*.cs

export interface DistrictOverview {
  id: number;
  name: string;
  stateCode?: string | null;
  activeSchoolCount: number;
  activeStaffCount: number;
}

export interface DistrictSchool {
  id: number;
  name: string;
  stateCode?: string | null;
  activeStudentCount: number;
  activeStaffCount: number;
}

export interface SaveSchoolRequest {
  name: string;
  stateCode?: string;
}

// Oversight dashboard aggregate (mirrors DistrictDashboardDto). DistrictAdmin
// sees the whole district; SchoolAdmin gets a server-sliced own-school view.

export interface DashboardSchool {
  id: number;
  name: string;
  activeStudentCount: number;
}

export interface DashboardStaffSummary {
  activeCount: number;
  deactivatedCount: number;
  invitedCount: number;
}

export type DashboardInviteStatus = 'pending' | 'expired';

export interface DashboardInvite {
  id: number;
  email: string;
  orgRoleId: number;
  orgRoleName: string;
  schoolId?: number | null;
  schoolName?: string | null;
  inviteExpiresAt: string;
  status: DashboardInviteStatus;
}

export interface DashboardStudent {
  schoolStudentId: number;
  firstName: string;
  lastName?: string | null;
  schoolName: string;
}

export interface DashboardNoParentStudent extends DashboardStudent {
  // True when a parent invite is pending; false means never invited.
  parentInvitePending: boolean;
}

export interface DistrictDashboard {
  schools: DashboardSchool[];
  staffSummary: DashboardStaffSummary;
  // Pending + expired invites, expired-first triage order.
  invitesNeedingAttention: DashboardInvite[];
  studentsWithoutStaff: DashboardStudent[];
  studentsWithoutParent: DashboardNoParentStudent[];
}
