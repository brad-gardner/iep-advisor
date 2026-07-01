import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import {
  registerDistrictAdmin,
  createSchoolViaApi,
  createStaffViaInvite,
  createStudentViaApi,
  grantStaffAccessViaApi,
  deactivateUsers,
  ORG_ROLE,
  type DistrictAdminAccount,
  type SchoolRef,
} from '../helpers/org-data';
import { LoginPage } from '../pages/login.page';

// Fresh context: seeds and logs in as brand-new org users; must not inherit the
// shared parent storage state.
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

const API_URL = process.env.API_URL || 'https://localhost:7200';
const SHOTS = '/tmp/iep-screenshots';

async function apiPost(path: string, token: string, body: unknown): Promise<any> {
  const res = await fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  });
  return res.json();
}

async function apiGet(path: string, token: string): Promise<number> {
  const res = await fetch(`${API_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  return res.status;
}

test.describe('District audit-log viewer', () => {
  let admin: DistrictAdminAccount;
  let school: SchoolRef;
  let teacher: Awaited<ReturnType<typeof createStaffViaInvite>>;
  let studentFirstName: string;
  const createdUserIds: number[] = [];

  test.beforeAll(async () => {
    const ts = Date.now();
    studentFirstName = `Audra${ts}`;

    admin = await registerDistrictAdmin();
    createdUserIds.push(admin.userId);
    school = await createSchoolViaApi(admin.token, `Audit School ${ts}`, 'OH');

    teacher = await createStaffViaInvite(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: school.id,
      firstName: 'Tess',
      lastName: 'Teacher',
    });
    createdUserIds.push(teacher.userId);

    const student = await createStudentViaApi(admin.token, {
      firstName: studentFirstName,
      schoolId: school.id,
    });
    const studentId = student.student!.id;

    // Give the teacher access, then have the teacher create and VIEW an IEP
    // draft — the GET writes an AccessAuditLog "View" row with the teacher as
    // actor, which the admin should then see in the audit log.
    await grantStaffAccessViaApi(admin.token, studentId, teacher.staffProfileId);
    const draft = await apiPost(
      `/api/educator/students/${studentId}/iep-drafts`,
      teacher.token,
      { title: 'Audit Trail Draft' }
    );
    const draftId = draft.data?.id;
    expect(draftId, 'draft should be created').toBeTruthy();
    await apiGet(`/api/iep-drafts/${draftId}`, teacher.token);
  });

  test.afterAll(async () => {
    const adminToken = await getAdminToken();
    await deactivateUsers(adminToken, createdUserIds);
  });

  test('admin sees the staff activity entry on the Activity log page', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login(admin.email, admin.password);
    await page.waitForFunction(
      () => !!localStorage.getItem('iep-assistant_token'),
      undefined,
      { timeout: 15000 }
    );

    // The "Activity log" nav item is visible to the admin.
    await page.goto('/educator');
    await expect(
      page.locator('[data-testid="nav-educator/admin/activity"]')
    ).toBeVisible({ timeout: 15000 });

    await page.goto('/educator/admin/activity');
    await expect(page.locator('[data-testid="audit-log-page"]')).toBeVisible({
      timeout: 15000,
    });

    // The teacher's draft-view shows up as an enriched row (actor name present).
    const firstRow = page.locator('[data-testid^="audit-row-"]').first();
    await expect(firstRow).toBeVisible({ timeout: 15000 });
    await expect(firstRow).toContainText('Tess');
    await page.screenshot({ path: `${SHOTS}/audit-log.png`, fullPage: true });
  });

  test('a Teacher sees neither the Activity log nav item nor the page', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login(teacher.email, teacher.password);
    await page.waitForFunction(
      () => !!localStorage.getItem('iep-assistant_token'),
      undefined,
      { timeout: 15000 }
    );

    await page.goto('/educator');
    await expect(
      page.locator('[data-testid="nav-educator/admin/activity"]')
    ).toHaveCount(0);

    // Direct navigation self-guards: the page renders an access-restricted
    // notice (no filters, no rows) rather than the audit log itself.
    await page.goto('/educator/admin/activity');
    await expect(page.getByText('Access restricted')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('[data-testid="audit-log-staff-filter"]')).toHaveCount(0);
  });
});
