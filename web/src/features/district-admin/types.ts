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

// Audit-log viewer (Phase 2). Mirrors api/IepAssistant.Api/DTOs/District/AuditLogDto.cs.

// The auditable actions, stored server-side as the enum's string name.
export const AUDIT_ACTIONS = ['View', 'Edit', 'Share', 'Export', 'Finalize'] as const;
export type AuditAction = (typeof AUDIT_ACTIONS)[number];

// One enriched audit row. Display fields (actorName, resourceDisplayName,
// recipientName) always carry server-side fallbacks — render them verbatim.
export interface AuditLogEntry {
  id: number;
  action: string;
  actorUserId: number;
  actorName: string;
  resourceType: string;
  resourceId: number;
  resourceDisplayName: string;
  recipientUserId?: number | null;
  recipientName?: string | null;
  createdAt: string;
}

// A keyset page: entries plus the cursor for the next page (null when exhausted).
export interface AuditLogPage {
  entries: AuditLogEntry[];
  nextCursor: number | null;
}

// Query filters. Date bounds are UTC instants (the filters component converts
// local-day boundaries; the upper bound is inclusive). cursor/pageSize drive
// keyset pagination. Only defined fields are serialized into the query string.
export interface AuditLogFilters {
  staffUserId?: number;
  studentId?: number;
  action?: AuditAction;
  fromUtc?: string;
  toUtc?: string;
  cursor?: number;
  pageSize?: number;
}
