import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { generateBetaCode, registerUser } from '../helpers/api';
import {
  registerDistrictAdmin,
  createSchoolViaApi,
  inviteStaffViaApi,
  deactivateUsers,
  ORG_ROLE,
  STAFF_PASSWORD,
  type DistrictAdminAccount,
  type SchoolRef,
} from '../helpers/org-data';
import { StaffAcceptPage } from '../pages/staff-accept.page';

// Fresh contexts throughout — the invite accept flow must not inherit the shared
// parent storage state.
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

const SHOTS = '/tmp/iep-screenshots';

test.describe('Staff invite acceptance', () => {
  let admin: DistrictAdminAccount;
  let school: SchoolRef;
  const createdUserIds: number[] = [];

  test.beforeAll(async () => {
    admin = await registerDistrictAdmin();
    createdUserIds.push(admin.userId);
    school = await createSchoolViaApi(admin.token, 'Invite Test Elementary');
  });

  test.afterAll(async () => {
    const adminToken = await getAdminToken();
    await deactivateUsers(adminToken, createdUserIds);
  });

  test('golden path: preview -> accept -> lands on educator dashboard', async ({ page }) => {
    const invite = await inviteStaffViaApi(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: school.id,
    });
    expect(invite.inviteToken, 'invite URL/token should be exposed in dev').toBeTruthy();

    const acceptPage = new StaffAcceptPage(page);
    await acceptPage.goto(invite.inviteToken!);
    await acceptPage.expectPreview({
      district: admin.districtName,
      role: 'Teacher',
      email: invite.email,
    });
    await page.screenshot({ path: `${SHOTS}/05-staff-accept-page.png`, fullPage: true });

    await acceptPage.fillAndSubmit({ firstName: 'Terry', lastName: 'Teacher', password: STAFF_PASSWORD });
    await acceptPage.expectLandsOnEducator();

    // Track the new user for cleanup.
    const me = await page.evaluate(async () => {
      const token = localStorage.getItem('iep-assistant_token');
      if (!token) return null;
      const res = await fetch('/api/auth/me', { headers: { Authorization: `Bearer ${token}` } });
      return res.json();
    });
    if (me?.data?.id) createdUserIds.push(me.data.id);
  });

  test('garbage token shows invalid message (no form)', async ({ page }) => {
    const acceptPage = new StaffAcceptPage(page);
    await acceptPage.goto('this-is-not-a-real-token');
    await acceptPage.expectInvalid();
  });

  test('reusing an already-claimed token shows invalid (no form)', async ({ page }) => {
    // Create a dedicated invite, accept it via the API, then try the SAME token in the UI.
    const invite = await inviteStaffViaApi(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: school.id,
    });
    expect(invite.inviteToken).toBeTruthy();

    // Claim it via the API.
    const claimRes = await fetch(`${process.env.API_URL || 'https://localhost:7200'}/api/staff-invites/accept`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        token: invite.inviteToken,
        firstName: 'First',
        lastName: 'Claim',
        password: STAFF_PASSWORD,
      }),
    });
    const claimBody = await claimRes.json();
    expect(claimBody.success).toBe(true);
    if (claimBody.data?.user?.id) createdUserIds.push(claimBody.data.user.id);

    // Now the UI should refuse the consumed token.
    const acceptPage = new StaffAcceptPage(page);
    await acceptPage.goto(invite.inviteToken!);
    await acceptPage.expectInvalid();
  });

  test('inviting an email that already has an account is rejected (API)', async () => {
    // Pre-register a parent account, then try to invite that same email as staff.
    const adminToken = await getAdminToken();
    const betaCode = await generateBetaCode(adminToken);
    const existingEmail = `existing-${Date.now()}@e2e.test`;
    const reg = await registerUser(existingEmail, STAFF_PASSWORD, 'Existing', 'Parent', betaCode);
    expect(reg.success).toBe(true);

    const invite = await inviteStaffViaApi(admin.token, {
      email: existingEmail,
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: school.id,
    });
    expect(invite.raw.success).toBe(false);
    expect(invite.raw.message?.toLowerCase()).toContain('already has an account');
  });
});
