import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import {
  registerDistrictAdmin,
  createSchoolViaApi,
  createStaffViaInvite,
  inviteStaffViaApi,
  createStudentViaApi,
  grantStaffAccessViaApi,
  deactivateUsers,
  ORG_ROLE,
  type DistrictAdminAccount,
  type SchoolRef,
} from '../helpers/org-data';
import { LoginPage } from '../pages/login.page';

// Fresh context: this seeds and logs in as a brand-new DistrictAdmin and must
// not inherit the shared parent storage state.
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

const SHOTS = '/tmp/iep-screenshots';

test.describe('District oversight dashboard', () => {
  let admin: DistrictAdminAccount;
  let school: SchoolRef;
  let staffedName: string;
  let orphanName: string;
  const createdUserIds: number[] = [];

  test.beforeAll(async () => {
    const ts = Date.now();
    staffedName = `Amy${ts}`;
    orphanName = `Ozzie${ts}`;

    admin = await registerDistrictAdmin();
    createdUserIds.push(admin.userId);
    school = await createSchoolViaApi(admin.token, `Dashboard School ${ts}`, 'OH');

    // A teacher who will be assigned to one student (so that student has staff).
    const teacher = await createStaffViaInvite(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: school.id,
      firstName: 'Tess',
      lastName: 'Teacher',
    });
    createdUserIds.push(teacher.userId);

    // Student WITH assigned staff — must NOT appear in the "no assigned staff" list.
    const staffed = await createStudentViaApi(admin.token, {
      firstName: staffedName,
      schoolId: school.id,
    });
    await grantStaffAccessViaApi(admin.token, staffed.student!.id, teacher.staffProfileId);

    // Student with NO staff and NO parent link — appears in both attention lists.
    await createStudentViaApi(admin.token, {
      firstName: orphanName,
      schoolId: school.id,
    });

    // A pending staff invite so the invites tile has content.
    await inviteStaffViaApi(admin.token, {
      orgRoleId: ORG_ROLE.Teacher,
      schoolId: school.id,
    });
  });

  test.afterAll(async () => {
    const adminToken = await getAdminToken();
    await deactivateUsers(adminToken, createdUserIds);
  });

  test('admin sees oversight tiles and the attention link filters the roster', async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login(admin.email, admin.password);

    // Land on the educator home where the dashboard tiles render.
    await page.waitForFunction(
      () => !!localStorage.getItem('iep-assistant_token'),
      undefined,
      { timeout: 15000 }
    );
    await page.goto('/educator');

    // --- All four tiles render -------------------------------------------------
    await expect(page.locator('[data-testid="district-dashboard-tiles"]')).toBeVisible({
      timeout: 15000,
    });
    await expect(page.locator('[data-testid="dashboard-schools-tile"]')).toBeVisible();
    await expect(page.locator('[data-testid="dashboard-staff-tile"]')).toBeVisible();
    await expect(page.locator('[data-testid="dashboard-invites-tile"]')).toBeVisible();
    await expect(page.locator('[data-testid="dashboard-attention-tile"]')).toBeVisible();

    // The invited-staff count reflects the one pending invite.
    await expect(page.locator('[data-testid="dashboard-staff-invited-count"]')).toHaveText('1');

    // The unstaffed student shows under "no assigned staff"; the staffed one does not.
    const noStaff = page.locator('[data-testid="dashboard-attention-no-staff"]');
    await expect(noStaff).toContainText(orphanName);
    await expect(noStaff).not.toContainText(staffedName);
    await page.screenshot({ path: `${SHOTS}/dashboard-tiles.png`, fullPage: true });

    // --- "View all" lands on the filtered roster ------------------------------
    await page.locator('[data-testid="dashboard-attention-no-staff-view-all"]').click();
    await page.waitForURL(/\/educator\/students\?attention=no-staff/, { timeout: 15000 });

    await expect(page.locator('[data-testid="attention-filter-indicator"]')).toBeVisible();
    const roster = page.locator('[data-testid="student-list"]');
    await expect(roster).toContainText(orphanName);
    await expect(roster).not.toContainText(staffedName);

    // Clearing the filter restores the full roster (staffed student reappears).
    await page.locator('[data-testid="attention-filter-clear"]').click();
    await expect(page.locator('[data-testid="attention-filter-indicator"]')).toHaveCount(0);
    await expect(roster).toContainText(staffedName);
  });
});
