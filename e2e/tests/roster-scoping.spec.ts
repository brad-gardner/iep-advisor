import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import {
  registerDistrictAdmin,
  createSchoolViaApi,
  createStaffViaInvite,
  createStudentViaApi,
  listStudentsViaApi,
  getStudentDetailStatus,
  grantStaffAccessViaApi,
  revokeStaffAccessForProfile,
  deactivateUsers,
  ORG_ROLE,
  type DistrictAdminAccount,
  type SchoolRef,
} from '../helpers/org-data';

test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

test.describe('Roster scoping & staff-access grants', () => {
  let admin: DistrictAdminAccount;
  let schoolA: SchoolRef;
  let schoolB: SchoolRef;
  const createdUserIds: number[] = [];

  test.beforeAll(async () => {
    admin = await registerDistrictAdmin();
    createdUserIds.push(admin.userId);
    schoolA = await createSchoolViaApi(admin.token, 'Roster School A');
    schoolB = await createSchoolViaApi(admin.token, 'Roster School B');
  });

  test.afterAll(async () => {
    const adminToken = await getAdminToken();
    await deactivateUsers(adminToken, createdUserIds);
  });

  test('teacher sees only granted students; grant then revoke toggles list & detail', async () => {
    // Teacher T1 in school A.
    const t1 = await createStaffViaInvite(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: schoolA.id,
      firstName: 'Tess',
      lastName: 'One',
    });
    createdUserIds.push(t1.userId);

    // Student S1 in school A, created by the district admin (requires schoolId).
    const { student: s1 } = await createStudentViaApi(admin.token, {
      firstName: 'Sam',
      lastName: 'A',
      schoolId: schoolA.id,
    });
    expect(s1).toBeTruthy();

    // Initially: T1 has no grant -> empty list, detail forbidden.
    let list = await listStudentsViaApi(t1.token);
    expect(list.data.length).toBe(0);
    expect(await getStudentDetailStatus(t1.token, s1!.id)).not.toBe(200);

    // Admin grants T1 access to S1.
    const grant = await grantStaffAccessViaApi(admin.token, s1!.id, t1.staffProfileId);
    expect(grant.success).toBe(true);

    // Now T1 sees S1 and can open detail.
    list = await listStudentsViaApi(t1.token);
    expect(list.data.map((s: any) => s.id)).toContain(s1!.id);
    expect(await getStudentDetailStatus(t1.token, s1!.id)).toBe(200);

    // Revoke -> list empty again, detail forbidden.
    const revoke = await revokeStaffAccessForProfile(admin.token, s1!.id, t1.staffProfileId);
    expect(revoke.success).toBe(true);

    list = await listStudentsViaApi(t1.token);
    expect(list.data.map((s: any) => s.id)).not.toContain(s1!.id);
    expect(await getStudentDetailStatus(t1.token, s1!.id)).not.toBe(200);
  });

  test('SchoolAdmin sees own-school students without explicit grants; not other school', async () => {
    // SchoolAdmin in school A.
    const sa = await createStaffViaInvite(admin.token, {
      orgRoleId: ORG_ROLE.SchoolAdmin,
      schoolId: schoolA.id,
      firstName: 'Sandy',
      lastName: 'Admin',
    });
    createdUserIds.push(sa.userId);

    // Student in school A and a student in school B.
    const { student: sA } = await createStudentViaApi(admin.token, {
      firstName: 'InA',
      schoolId: schoolA.id,
    });
    const { student: sB } = await createStudentViaApi(admin.token, {
      firstName: 'InB',
      schoolId: schoolB.id,
    });
    expect(sA).toBeTruthy();
    expect(sB).toBeTruthy();

    // School-A admin sees the school-A student with no grant...
    const list = await listStudentsViaApi(sa.token);
    const ids = list.data.map((s: any) => s.id);
    expect(ids).toContain(sA!.id);
    // ...and does NOT see the school-B student.
    expect(ids).not.toContain(sB!.id);
    // Detail for the school-B student is forbidden.
    expect(await getStudentDetailStatus(sa.token, sB!.id)).not.toBe(200);
  });
});
