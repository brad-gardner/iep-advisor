import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import {
  registerDistrictAdmin,
  createSchoolViaApi,
  createStaffViaInvite,
  inviteStaffViaApi,
  revokeInviteViaApi,
  deactivateStaffViaApi,
  reactivateStaffViaApi,
  deactivateUsers,
  rawStatus,
  ORG_ROLE,
  STAFF_PASSWORD,
  type DistrictAdminAccount,
  type SchoolRef,
} from '../helpers/org-data';

// All-API spec — no browser/storage state needed, but keep a fresh context to be safe.
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

const API_URL = process.env.API_URL || 'https://localhost:7200';

async function loginToken(email: string, password: string): Promise<string> {
  const res = await fetch(`${API_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  const body = await res.json();
  return body.data?.token;
}

test.describe('Org management: scoping, revoke, deactivation, last-admin guard', () => {
  let admin: DistrictAdminAccount;
  let schoolA: SchoolRef;
  let schoolB: SchoolRef;
  const createdUserIds: number[] = [];

  test.beforeAll(async () => {
    admin = await registerDistrictAdmin();
    createdUserIds.push(admin.userId);
    schoolA = await createSchoolViaApi(admin.token, 'Org School A');
    schoolB = await createSchoolViaApi(admin.token, 'Org School B');
  });

  test.afterAll(async () => {
    const adminToken = await getAdminToken();
    await deactivateUsers(adminToken, createdUserIds);
  });

  test('SchoolAdmin cannot invite into another school', async () => {
    const schoolAdmin = await createStaffViaInvite(admin.token, {
      orgRoleId: ORG_ROLE.SchoolAdmin,
      schoolId: schoolA.id,
      firstName: 'Sara',
      lastName: 'SchoolAdmin',
    });
    createdUserIds.push(schoolAdmin.userId);

    const crossSchool = await inviteStaffViaApi(schoolAdmin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: schoolB.id,
    });
    expect(crossSchool.raw.success).toBe(false);
    expect(crossSchool.raw.message?.toLowerCase()).toContain('permission');

    // ...and cannot invite a DistrictAdmin.
    const elevate = await inviteStaffViaApi(schoolAdmin.token, {
      orgRoleId: ORG_ROLE.DistrictAdmin,
    });
    expect(elevate.raw.success).toBe(false);
    expect(elevate.raw.message?.toLowerCase()).toContain('permission');

    // A SchoolAdmin CAN revoke a pending invite in their own school.
    const pending = await inviteStaffViaApi(schoolAdmin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: schoolA.id,
    });
    expect(pending.raw.success).toBe(true);
    const revoked = await revokeInviteViaApi(schoolAdmin.token, pending.id);
    expect(revoked.success).toBe(true);
  });

  test('deactivating a teacher invalidates their existing JWT (401 on next call)', async () => {
    const teacher = await createStaffViaInvite(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: schoolA.id,
      firstName: 'Tom',
      lastName: 'Teacher',
    });
    createdUserIds.push(teacher.userId);

    // The freshly-minted JWT works.
    expect(await rawStatus('GET', '/api/educator/me', teacher.token)).toBe(200);

    // Deactivate via the district admin.
    const deact = await deactivateStaffViaApi(admin.token, teacher.staffProfileId);
    expect(deact.success).toBe(true);

    // The teacher's OLD token is now rejected (SecurityStamp bump).
    expect(await rawStatus('GET', '/api/educator/me', teacher.token)).toBe(401);

    // Reactivate; a FRESH login then works again. (The old token's stamp stays stale.)
    const react = await reactivateStaffViaApi(admin.token, teacher.staffProfileId);
    expect(react.success).toBe(true);
    const freshToken = await loginToken(teacher.email, STAFF_PASSWORD);
    expect(freshToken).toBeTruthy();
    expect(await rawStatus('GET', '/api/educator/me', freshToken)).toBe(200);
  });

  test('last active DistrictAdmin cannot deactivate themselves', async () => {
    const selfDeact = await deactivateStaffViaApi(admin.token, admin.staffProfileId);
    expect(selfDeact.success).toBe(false);
    expect(selfDeact.message?.toLowerCase()).toContain('last active district admin');
  });
});
