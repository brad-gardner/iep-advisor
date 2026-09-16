// Mirrors api/IepAssistant.Api/DTOs/Home/*.cs (see plan5-contract.md). One
// role-scoped, server-computed home per user — a single `GET /api/home`
// discriminated on `kind`, rather than many client-side fetches.
import type { InviteStatus, MeetingStatus, MeetingType } from '@/features/meetings/types';
import type { ObligationDto, ObligationKind, ObligationStatus } from '@/features/obligations/types';
import type { ComplianceSummaryDto } from '@/features/district-admin/types';

export type HomeKind = 'Staff' | 'Parent' | 'Student';

// Derived from the caller's OrgRole (Teacher→CaseManager, RelatedServiceProvider→Provider,
// GeneralEducator→GeneralEducator) plus the two admin tiers.
export const STAFF_HOME_VARIANTS = [
  'CaseManager',
  'Provider',
  'GeneralEducator',
  'SchoolAdmin',
  'DistrictAdmin',
] as const;
export type StaffHomeVariant = (typeof STAFF_HOME_VARIANTS)[number];

export interface HomeMeetingDto {
  id: number;
  title: string;
  type: MeetingType;
  startsAtUtc: string;
  timeZoneId: string;
  durationMinutes: number;
  studentId: number;
  studentName: string;
  myInviteStatus: InviteStatus | null;
  status: MeetingStatus;
}

// Same shape as the plan-4 obligations feed.
export type HomeObligationDto = ObligationDto;

export interface HomeDraftDto {
  instanceId: number;
  studentId: number;
  studentName: string;
  documentTypeKey: string;
  documentTypeDisplayName: string;
  lastEditedAt: string;
  completenessPercent: number;
  requiredMissing: number;
}

// Plan 6 shape — always `[]` until family sharing ships. Rendered empty-safe.
export interface HomeSharedDraftDto {
  instanceId: number;
  studentId: number;
  studentName: string;
  sharedAt: string;
  respondedAt: string | null;
}

// Plan 7 shape — always `[]` until provider requests ship.
export interface HomeProviderRequestDto {
  id: number;
  studentId: number;
  studentName: string;
  dueDate: string;
}

// Plan 7 shape — always `[]` until finalize-signing ships.
export interface HomeUnsignedDto {
  versionId: number;
  studentId: number;
  studentName: string;
  finalizedAt: string;
}

export interface RosterAttentionDto {
  noLead: number;
  noFamily: number;
  unknownDates: number;
  overdueAnnual: number;
  overdueReeval: number;
  due30: number;
}

// A procedural-deadline row for the admin "overdue/at-risk" table — sorted by
// STUDENT name by the server (and by default here), never by staff.
export interface CaseManagerRowDto {
  studentId: number;
  studentName: string;
  caseManagerName: string | null;
  kind: ObligationKind;
  dueDate: string | null;
  status: ObligationStatus;
}

export interface StaffHomeDto {
  variant: StaffHomeVariant;
  displayName: string;
  scopeLabel: string;
  weekStart: string;
  weekEnd: string;
  meetingsThisWeek: HomeMeetingDto[];
  obligations: HomeObligationDto[];
  drafts: HomeDraftDto[];
  sharedDraftsAwaitingFamily: HomeSharedDraftDto[];
  familyResponsesToReview: HomeSharedDraftDto[];
  providerRequestsIOwe: HomeProviderRequestDto[];
  // Admins only.
  rosterAttention?: RosterAttentionDto | null;
  // SchoolAdmin/DistrictAdmin only.
  overdueByCaseManager?: CaseManagerRowDto[] | null;
  // Empty-safe (plan 7).
  unsignedFinalized?: HomeUnsignedDto[] | null;
  // DistrictAdmin only — same numbers as the compliance board with no filters.
  complianceSummary?: ComplianceSummaryDto | null;
}

export interface ParentChildDto {
  childId: number;
  childName: string;
  hasSchoolLink: boolean;
  studentId: number | null;
}

export type ParentDocumentKind = 'Finalized' | 'SharedDraft';

export interface ParentDocumentDto {
  kind: ParentDocumentKind;
  id: number;
  childId: number;
  childName: string;
  documentTypeDisplayName: string;
  versionNumber: number | null;
  date: string;
  linkPath: string;
}

export interface ParentProgressReportDto {
  id: number;
  childId: number;
  childName: string;
  title: string;
  createdAt: string;
}

export interface ParentNextMeetingDto extends HomeMeetingDto {
  childId: number;
  childName: string;
  daysUntil: number;
}

export interface ParentHomeDto {
  displayName: string;
  nextMeeting: ParentNextMeetingDto | null;
  documentsToReview: ParentDocumentDto[];
  children: ParentChildDto[];
  recentProgressReports: ParentProgressReportDto[];
  // Demoted setup notices, e.g. "No school link yet".
  setupNotices: string[];
}

export interface StudentHomeDto {
  displayName: string;
  nextMeeting: HomeMeetingDto | null;
  workspaceNudge: string | null;
  linkedStudentId: number | null;
}

export interface HomeDto {
  kind: HomeKind;
  generatedAt: string;
  staff?: StaffHomeDto | null;
  parent?: ParentHomeDto | null;
  student?: StudentHomeDto | null;
}
