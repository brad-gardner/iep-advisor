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

// Compliance board (plan 5). Mirrors api/IepAssistant.Api/DTOs/District/ComplianceBoardDto.cs.
// `due30`/`due60` = due within N days from today (not overdue); overdue
// buckets apply regardless of the selected date range.

export interface ComplianceSummaryDto {
  overdueAnnual: number;
  overdueReeval: number;
  due30: number;
  due60: number;
  unknownDates: number;
  noLead: number;
  activeStudents: number;
}

export interface ComplianceSchoolRowDto {
  schoolId: number;
  schoolName: string;
  activeStudents: number;
  overdueAnnual: number;
  overdueReeval: number;
  due30: number;
  due60: number;
  unknownDates: number;
  noLead: number;
}

// `drill` maps each summary/row count key (e.g. "overdueAnnual") to a roster
// query string fragment (e.g. "attention=OverdueAnnual") — see
// `lib/drill-link.ts` for how the UI turns that into a roster link.
export interface ComplianceBoardDto {
  generatedAt: string;
  from: string;
  to: string;
  summary: ComplianceSummaryDto;
  bySchool: ComplianceSchoolRowDto[];
  drill: Record<string, string>;
}

export interface AdoptionSchoolRow {
  schoolId: number;
  schoolName: string;
  staffActive: number;
  staffTotal: number;
  draftsStarted: number;
  draftsFinalized: number;
}

export interface AdoptionDto {
  days: number;
  staffActiveLast14: number;
  staffTotal: number;
  bySchool: AdoptionSchoolRow[];
  draftsStarted: number;
  draftsFinalized: number;
  // States the server's "active" predicate for display, e.g. "Notification
  // read, document edit, meeting created/RSVP'd in the window".
  activeRule: string;
}

export interface EngagementSchoolRow {
  schoolId: number;
  schoolName: string;
  studentsWithFamilyLink: number;
  activeStudents: number;
}

// Plan 6 numbers (`draftsShared`/`responsesReceived`) are 0 until family
// sharing ships; the shape is fixed now.
export interface EngagementDto {
  studentsWithFamilyLink: number;
  activeStudents: number;
  draftsShared: number;
  responsesReceived: number;
  bySchool: EngagementSchoolRow[];
}
