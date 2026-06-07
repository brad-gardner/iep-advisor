import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { deactivateUsers } from '../helpers/org-data';
import { DistrictRegisterPage } from '../pages/district-register.page';
import { DistrictSetupWizardPage } from '../pages/district-setup.page';

// Fresh context: the district-signup flow registers a brand-new DistrictAdmin
// and must NOT inherit the shared parent storage state.
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

const SHOTS = '/tmp/iep-screenshots';

test.describe('District self-serve signup (golden path)', () => {
  const createdUserIds: number[] = [];

  test.afterAll(async () => {
    if (createdUserIds.length === 0) return;
    const adminToken = await getAdminToken();
    await deactivateUsers(adminToken, createdUserIds);
  });

  test('register district -> wizard -> create school -> invite teacher -> dashboard', async ({ page }) => {
    const ts = Date.now();
    const email = `district-admin-ui-${ts}@e2e.test`;
    const teacherEmail = `teacher-ui-${ts}@e2e.test`;

    const registerPage = new DistrictRegisterPage(page);
    const wizard = new DistrictSetupWizardPage(page);

    // --- Register: chooser visible, pick district, fill + submit -------------
    await registerPage.goto();
    await registerPage.expectChooserVisible();
    await page.screenshot({ path: `${SHOTS}/01-register-chooser.png`, fullPage: true });

    await registerPage.chooseDistrict();
    await registerPage.fillDistrictForm({
      firstName: 'Dana',
      lastName: 'Admin',
      email,
      districtName: `E2E Signup District ${ts}`,
      state: 'OH',
      password: 'TestPass123!',
    });
    await page.screenshot({ path: `${SHOTS}/02-district-form.png`, fullPage: true });
    await registerPage.submitDistrict();

    // --- Wizard: welcome -> create school -----------------------------------
    await page.waitForURL(/\/educator\/setup/, { timeout: 15000 });
    await wizard.expectWelcome();
    await wizard.start();
    await page.screenshot({ path: `${SHOTS}/03-wizard-school-step.png`, fullPage: true });

    await wizard.createSchool(`Roosevelt Elementary ${ts}`, 'OH');
    await wizard.toStaffStep();

    // --- Wizard: invite a teacher; assert the copyable invite URL appears ----
    await wizard.inviteTeacher(teacherEmail);
    const inviteUrl = await wizard.expectInviteUrl();
    expect(inviteUrl).toContain('/staff/accept-invite?token=');
    await page.screenshot({ path: `${SHOTS}/04-wizard-staff-invite-url.png`, fullPage: true });

    // --- Finish -> educator dashboard with district overview card -----------
    await wizard.toDone();
    await wizard.finish();
    await expect(page.locator('[data-testid="district-overview-card"]')).toBeVisible({ timeout: 15000 });
    await page.screenshot({ path: `${SHOTS}/06-educator-dashboard-overview.png`, fullPage: true });

    // Track the new admin user for cleanup (read its id from the token-backed me).
    const me = await page.evaluate(async () => {
      const token = localStorage.getItem('iep-assistant_token');
      if (!token) return null;
      const res = await fetch('/api/auth/me', { headers: { Authorization: `Bearer ${token}` } });
      return res.json();
    });
    if (me?.data?.id) createdUserIds.push(me.data.id);
  });
});
