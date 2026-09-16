// Mirrors api/IepAssistant.Api/DTOs/Educator/*.cs

// Org role IDs are stable, seeded server-side (OrgRoleIds): 1=DistrictAdmin,
// 2=SchoolAdmin, 3=Teacher, 4=RelatedServiceProvider, 5=GeneralEducator.
// Roles 3–5 share the non-admin authz tier; RelatedServiceProvider may
// additionally hold access to students in any active school of the district.
export const ORG_ROLE = {
  DistrictAdmin: 1,
  SchoolAdmin: 2,
  Teacher: 3,
  RelatedServiceProvider: 4,
  GeneralEducator: 5,
} as const;

export type OrgRoleId = (typeof ORG_ROLE)[keyof typeof ORG_ROLE];

/** DistrictAdmin or SchoolAdmin — the tier that may manage rosters/teams. */
export function isAdminOrgRole(orgRoleId: number | null | undefined): boolean {
  return orgRoleId === ORG_ROLE.DistrictAdmin || orgRoleId === ORG_ROLE.SchoolAdmin;
}

/** Teacher / RelatedServiceProvider / GeneralEducator — the caseload tier. */
export function isCaseloadOrgRole(orgRoleId: number | null | undefined): boolean {
  return (
    orgRoleId === ORG_ROLE.Teacher ||
    orgRoleId === ORG_ROLE.RelatedServiceProvider ||
    orgRoleId === ORG_ROLE.GeneralEducator
  );
}

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

// ---------------------------------------------------------------- Controlled values
// Enums serialize as their string names (global JsonStringEnumConverter). The
// *_LABELS maps are for DISPLAY ONLY — never compare against labels.

export const GRADE_LEVELS = [
  'PK',
  'K',
  'G1',
  'G2',
  'G3',
  'G4',
  'G5',
  'G6',
  'G7',
  'G8',
  'G9',
  'G10',
  'G11',
  'G12',
  'Ungraded',
] as const;
export type GradeLevel = (typeof GRADE_LEVELS)[number];
export const GRADE_LEVEL_LABELS: Record<GradeLevel, string> = {
  PK: 'PK',
  K: 'K',
  G1: '1',
  G2: '2',
  G3: '3',
  G4: '4',
  G5: '5',
  G6: '6',
  G7: '7',
  G8: '8',
  G9: '9',
  G10: '10',
  G11: '11',
  G12: '12',
  Ungraded: 'Ungraded',
};

export const DISABILITY_CATEGORIES = [
  'Autism',
  'DeafBlindness',
  'Deafness',
  'DevelopmentalDelay',
  'EmotionalDisturbance',
  'HearingImpairment',
  'IntellectualDisability',
  'MultipleDisabilities',
  'OrthopedicImpairment',
  'OtherHealthImpairment',
  'SpecificLearningDisability',
  'SpeechOrLanguageImpairment',
  'TraumaticBrainInjury',
  'VisualImpairment',
  'Other',
] as const;
export type DisabilityCategory = (typeof DISABILITY_CATEGORIES)[number];
export const DISABILITY_CATEGORY_LABELS: Record<DisabilityCategory, string> = {
  Autism: 'Autism',
  DeafBlindness: 'Deaf-blindness',
  Deafness: 'Deafness',
  DevelopmentalDelay: 'Developmental delay',
  EmotionalDisturbance: 'Emotional disturbance',
  HearingImpairment: 'Hearing impairment',
  IntellectualDisability: 'Intellectual disability',
  MultipleDisabilities: 'Multiple disabilities',
  OrthopedicImpairment: 'Orthopedic impairment',
  OtherHealthImpairment: 'Other health impairment',
  SpecificLearningDisability: 'Specific learning disability',
  SpeechOrLanguageImpairment: 'Speech or language impairment',
  TraumaticBrainInjury: 'Traumatic brain injury',
  VisualImpairment: 'Visual impairment (including blindness)',
  Other: 'Other',
};

export const STUDENT_STATUSES = ['Active', 'Exited', 'Archived'] as const;
export type StudentStatus = (typeof STUDENT_STATUSES)[number];
export const STUDENT_STATUS_LABELS: Record<StudentStatus, string> = {
  Active: 'Active',
  Exited: 'Exited',
  Archived: 'Archived',
};

export const EXIT_REASONS = [
  'Graduated',
  'Transferred',
  'Withdrawn',
  'Declassified',
  'Other',
] as const;
export type ExitReason = (typeof EXIT_REASONS)[number];
export const EXIT_REASON_LABELS: Record<ExitReason, string> = {
  Graduated: 'Graduated',
  Transferred: 'Transferred',
  Withdrawn: 'Withdrawn',
  Declassified: 'Declassified',
  Other: 'Other',
};

export const TEAM_ROLES = [
  'CaseManager',
  'InterventionSpecialist',
  'GeneralEducationTeacher',
  'SpeechLanguagePathologist',
  'OccupationalTherapist',
  'PhysicalTherapist',
  'SchoolPsychologist',
  'Counselor',
  'LeaRepresentative',
  'Interpreter',
  'Other',
] as const;
export type TeamRole = (typeof TEAM_ROLES)[number];
export const TEAM_ROLE_LABELS: Record<TeamRole, string> = {
  CaseManager: 'Case manager',
  InterventionSpecialist: 'Intervention specialist',
  GeneralEducationTeacher: 'General education teacher',
  SpeechLanguagePathologist: 'Speech-language pathologist',
  OccupationalTherapist: 'Occupational therapist',
  PhysicalTherapist: 'Physical therapist',
  SchoolPsychologist: 'School psychologist',
  Counselor: 'Counselor',
  LeaRepresentative: 'LEA representative',
  Interpreter: 'Interpreter',
  Other: 'Other',
};

// Per-student access roles (mirrors AccessRole on the backend), serialized as
// their string name in the grant/list DTOs.
export type AccessRole = 'Viewer' | 'Collaborator' | 'Owner';

export const ACCESS_ROLES: AccessRole[] = ['Viewer', 'Collaborator', 'Owner'];

/**
 * The permission a team member gets when no explicit override is chosen —
 * mirrors the server default (CaseManager→Owner, LEA rep/Interpreter→Viewer,
 * everyone else→Collaborator). Display-only hint for the add-member form.
 */
export function defaultAccessRoleForTeamRole(teamRole: TeamRole): AccessRole {
  if (teamRole === 'CaseManager') return 'Owner';
  if (teamRole === 'LeaRepresentative' || teamRole === 'Interpreter') return 'Viewer';
  return 'Collaborator';
}

// ---------------------------------------------------------------- Students

export interface SchoolStudent {
  id: number;
  schoolId: number;
  schoolName?: string | null;
  firstName: string;
  lastName?: string | null;
  dateOfBirth?: string | null;
  stateCode?: string | null;
  externalStudentId?: string | null;
  gradeLevel?: GradeLevel | null;
  disabilityCategory?: DisabilityCategory | null;
  legacyDisabilityText?: string | null;
  homeLanguage?: string | null;
  status: StudentStatus;
  exitedAt?: string | null;
  exitReason?: ExitReason | null;
  caseManagerUserId?: number | null;
  caseManagerName?: string | null;
  iepDate?: string | null;
  annualReviewDueDate?: string | null;
  etrDate?: string | null;
  reevaluationDueDate?: string | null;
  // Kept for compatibility: equals `status === 'Active'`.
  isActive: boolean;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// `All` is a filter value only — never a stored status.
export type StudentStatusFilter = StudentStatus | 'All';

// Server-side "needs attention" narrowing (same predicates as the dashboard
// tiles and the plan-5 compliance board): no active lead case manager / no
// accepted parent link / procedural-deadline buckets. `Due30`/`Due60` are due
// within N days from today (not overdue); `UnknownDates` means the source
// dates needed to compute a deadline are missing — never rendered as healthy.
// `DueInRange` is the compliance board's caller-chosen `from`/`to` window (see
// `use-roster-query.ts`'s `from`/`to` params) — distinct from the fixed
// `Due30`/`Due60` buckets. Composes with the other filters and normal paging.
export const ATTENTION_FILTERS = [
  'NoCaseManager',
  'NoLinkedParent',
  'OverdueAnnual',
  'OverdueReeval',
  'Due30',
  'Due60',
  'UnknownDates',
  'DueInRange',
] as const;
export type AttentionFilter = (typeof ATTENTION_FILTERS)[number];
// `DueInRange`'s label is dynamic (built from the `from`/`to` query params by
// the roster page) — this fallback only covers a `DueInRange` deep link with
// no date bounds attached.
export const ATTENTION_FILTER_LABELS: Record<AttentionFilter, string> = {
  NoCaseManager: 'no case manager',
  NoLinkedParent: 'no linked parent',
  OverdueAnnual: 'an overdue annual review',
  OverdueReeval: 'an overdue reevaluation',
  Due30: 'a review due within 30 days',
  Due60: 'a review due within 60 days',
  UnknownDates: 'unknown dates',
  DueInRange: 'a review due in the selected range',
};

export interface StudentSearchParams {
  query?: string;
  schoolId?: number;
  status?: StudentStatusFilter;
  grade?: GradeLevel;
  attention?: AttentionFilter;
  // `yyyy-MM-dd` bounds for `attention: 'DueInRange'` only.
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateSchoolStudentRequest {
  firstName: string;
  lastName?: string;
  dateOfBirth?: string;
  stateCode?: string;
  externalStudentId?: string;
  gradeLevel?: GradeLevel;
  disabilityCategory?: DisabilityCategory;
  homeLanguage?: string;
  iepDate?: string;
  annualReviewDueDate?: string;
  etrDate?: string;
  reevaluationDueDate?: string;
  // REQUIRED when the caller is a DistrictAdmin; omitted for SchoolAdmin/Teacher.
  schoolId?: number;
}

// Full-replacement PUT body: every editable field is sent, null clears.
export interface UpdateSchoolStudentRequest {
  firstName: string;
  lastName: string | null;
  dateOfBirth: string | null;
  stateCode: string | null;
  externalStudentId: string | null;
  gradeLevel: GradeLevel | null;
  disabilityCategory: DisabilityCategory | null;
  homeLanguage: string | null;
  iepDate: string | null;
  annualReviewDueDate: string | null;
  etrDate: string | null;
  reevaluationDueDate: string | null;
}

export interface ExitStudentRequest {
  exitReason: ExitReason;
  exitedAt?: string;
}

export interface TransferStudentRequest {
  newSchoolId: number;
}

export interface AssignCaseManagerBulkRequest {
  studentIds: number[];
  userId: number;
}

export interface AssignCaseManagerBulkResult {
  updated: number;
}

// ---------------------------------------------------------------- IEP team

// Mirrors StudentTeamMemberDto. `accessRole` is the effective permission from
// the member's SchoolStudentAccess row — separate from their functional role.
export interface StudentTeamMember {
  id: number;
  userId: number;
  staffProfileId: number;
  firstName: string;
  lastName: string;
  email: string;
  orgRoleName: string;
  teamRole: TeamRole;
  isLead: boolean;
  accessRole: AccessRole;
  isActive: boolean;
  addedAt: string;
}

// Mirrors EligibleStaffDto: active staff who may join this student's team
// (same school, non-DistrictAdmin) plus RelatedServiceProviders anywhere in the
// district, excluding current active members. Server-filtered, so the picker
// never offers a row the API would reject.
export interface EligibleStaff {
  staffProfileId: number;
  userId: number;
  firstName: string;
  lastName: string;
  email: string;
  orgRoleId: number;
  orgRoleName: string;
  schoolId?: number | null;
  schoolName?: string | null;
}

export interface AddTeamMemberRequest {
  staffProfileId: number;
  teamRole: TeamRole;
  isLead?: boolean;
  // Omit to take the server default for the team role.
  accessRole?: AccessRole;
}

export interface UpdateTeamMemberRequest {
  teamRole?: TeamRole;
  accessRole?: AccessRole;
}

// ---------------------------------------------------------------- Family links

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
