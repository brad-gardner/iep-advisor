import { ORG_ROLE } from '../types';
import type { EducatorProfile, SchoolStudent, StudentTeamMember } from '../types';

export function makeProfile(overrides: Partial<EducatorProfile> = {}): EducatorProfile {
  return {
    staffProfileId: 1,
    userId: 1,
    orgRoleId: ORG_ROLE.DistrictAdmin,
    orgRoleName: 'DistrictAdmin',
    districtId: 1,
    districtName: 'Test District',
    schoolId: null,
    schoolName: null,
    isActive: true,
    stateCode: 'OH',
    title: null,
    credentials: null,
    ...overrides,
  };
}

export function makeStudent(overrides: Partial<SchoolStudent> = {}): SchoolStudent {
  return {
    id: 10,
    schoolId: 5,
    schoolName: 'Lincoln Elementary',
    firstName: 'Ada',
    lastName: 'Lovelace',
    dateOfBirth: '2015-03-04',
    stateCode: 'OH',
    externalStudentId: '000123',
    gradeLevel: 'G5',
    disabilityCategory: 'SpecificLearningDisability',
    legacyDisabilityText: null,
    homeLanguage: 'en',
    status: 'Active',
    exitedAt: null,
    exitReason: null,
    caseManagerUserId: 7,
    caseManagerName: 'Casey Manager',
    iepDate: '2026-01-10',
    annualReviewDueDate: '2027-01-10',
    etrDate: null,
    reevaluationDueDate: null,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

export function makeMember(overrides: Partial<StudentTeamMember> = {}): StudentTeamMember {
  return {
    id: 100,
    userId: 7,
    staffProfileId: 70,
    firstName: 'Casey',
    lastName: 'Manager',
    email: 'casey@district.org',
    orgRoleName: 'Teacher',
    teamRole: 'CaseManager',
    isLead: true,
    accessRole: 'Owner',
    isActive: true,
    addedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}
