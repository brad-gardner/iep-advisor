import type {
  CaseManagerRowDto,
  HomeDraftDto,
  HomeDto,
  HomeKind,
  HomeMeetingDto,
  HomeObligationDto,
  HomeSharedDraftDto,
  HomeUnsignedDto,
  ParentHomeDto,
  RosterAttentionDto,
  StaffHomeDto,
  StudentHomeDto,
} from '../types';
import type { ComplianceSummaryDto } from '@/features/district-admin/types';

export function makeComplianceSummary(
  overrides: Partial<ComplianceSummaryDto> = {}
): ComplianceSummaryDto {
  return {
    overdueAnnual: 3,
    overdueReeval: 2,
    due30: 5,
    due60: 8,
    unknownDates: 1,
    noLead: 2,
    activeStudents: 120,
    dueInRange: 6,
    ...overrides,
  };
}

export function makeHomeMeeting(overrides: Partial<HomeMeetingDto> = {}): HomeMeetingDto {
  return {
    id: 100,
    title: 'Annual review meeting',
    type: 'AnnualReview',
    startsAtUtc: '2099-10-01T15:00:00.000Z',
    timeZoneId: 'America/New_York',
    durationMinutes: 60,
    studentId: 10,
    studentName: 'Ada Lovelace',
    myInviteStatus: 'Pending',
    status: 'Scheduled',
    ...overrides,
  };
}

export function makeHomeObligation(overrides: Partial<HomeObligationDto> = {}): HomeObligationDto {
  return {
    kind: 'AnnualReview',
    dueDate: '2026-10-15',
    status: 'DueSoon',
    sourceLabel: 'IEP date',
    ownerUserId: 1,
    ownerName: 'Casey Manager',
    schoolStudentId: 10,
    studentName: 'Ada Lovelace',
    daysUntilDue: 20,
    ruleProfile: 'Default',
    ...overrides,
  };
}

export function makeHomeDraft(overrides: Partial<HomeDraftDto> = {}): HomeDraftDto {
  return {
    instanceId: 200,
    studentId: 10,
    studentName: 'Ada Lovelace',
    documentTypeKey: 'iep',
    documentTypeDisplayName: 'IEP',
    lastEditedAt: '2026-09-10T12:00:00.000Z',
    completenessPercent: 62,
    requiredMissing: 3,
    ...overrides,
  };
}

export function makeSharedDraft(overrides: Partial<HomeSharedDraftDto> = {}): HomeSharedDraftDto {
  return {
    instanceId: 300,
    studentId: 10,
    studentName: 'Ada Lovelace',
    sharedAt: '2026-09-05T12:00:00.000Z',
    respondedAt: null,
    ...overrides,
  };
}

export function makeCaseManagerRow(overrides: Partial<CaseManagerRowDto> = {}): CaseManagerRowDto {
  return {
    studentId: 10,
    studentName: 'Ada Lovelace',
    caseManagerName: 'Casey Manager',
    kind: 'AnnualReview',
    dueDate: '2026-09-01',
    status: 'Overdue',
    ...overrides,
  };
}

export function makeUnsignedFinalized(overrides: Partial<HomeUnsignedDto> = {}): HomeUnsignedDto {
  return {
    versionId: 400,
    studentId: 10,
    studentName: 'Ada Lovelace',
    finalizedAt: '2026-08-20T12:00:00.000Z',
    ...overrides,
  };
}

export function makeRosterAttention(overrides: Partial<RosterAttentionDto> = {}): RosterAttentionDto {
  return {
    noLead: 2,
    noFamily: 3,
    unknownDates: 1,
    overdueAnnual: 4,
    overdueReeval: 1,
    due30: 5,
    ...overrides,
  };
}

export function makeStaffHome(overrides: Partial<StaffHomeDto> = {}): StaffHomeDto {
  return {
    variant: 'CaseManager',
    displayName: 'Casey Manager',
    scopeLabel: 'Lincoln Elementary',
    weekStart: '2026-09-14',
    weekEnd: '2026-09-20',
    meetingsThisWeek: [],
    obligations: [],
    drafts: [],
    sharedDraftsAwaitingFamily: [],
    familyResponsesToReview: [],
    providerRequestsIOwe: [],
    ...overrides,
  };
}

export function makeParentHome(overrides: Partial<ParentHomeDto> = {}): ParentHomeDto {
  return {
    displayName: 'Priya Parent',
    nextMeeting: null,
    documentsToReview: [],
    children: [{ childId: 1, childName: 'Ada Lovelace', hasSchoolLink: true, studentId: 10 }],
    recentProgressReports: [],
    setupNotices: [],
    ...overrides,
  };
}

export function makeStudentHome(overrides: Partial<StudentHomeDto> = {}): StudentHomeDto {
  return {
    displayName: 'Ada Lovelace',
    nextMeeting: null,
    workspaceNudge: null,
    linkedStudentId: 10,
    ...overrides,
  };
}

// A loose override bag rather than `Partial<HomeDto>` — `HomeDto` is a real
// discriminated union, so `Partial` of it doesn't distribute the way a test
// fixture wants (each branch's own field would need its own partial). Pass
// `kind` plus a full DTO for whichever branch it selects; the other two are
// ignored (kept optional so existing `staff: undefined` call sites are unaffected).
interface HomeDtoOverrides {
  kind?: HomeKind;
  generatedAt?: string;
  staff?: StaffHomeDto;
  parent?: ParentHomeDto;
  student?: StudentHomeDto;
}

export function makeHomeDto(overrides: HomeDtoOverrides = {}): HomeDto {
  const kind = overrides.kind ?? 'Staff';
  const generatedAt = overrides.generatedAt ?? '2026-09-16T00:00:00.000Z';
  if (kind === 'Parent') {
    return { kind, generatedAt, parent: overrides.parent ?? makeParentHome() };
  }
  if (kind === 'Student') {
    return { kind, generatedAt, student: overrides.student ?? makeStudentHome() };
  }
  return { kind: 'Staff', generatedAt, staff: overrides.staff ?? makeStaffHome() };
}
