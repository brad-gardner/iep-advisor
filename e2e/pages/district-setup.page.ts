import { Page, expect } from '@playwright/test';

// First-run wizard at /educator/setup: welcome -> create school -> invite staff
// -> done. Reuses SchoolForm (testIdPrefix "district-setup-school") and the
// shared staff InviteForm (testid prefix "district-staff-invite-*").
export class DistrictSetupWizardPage {
  constructor(private page: Page) {}

  async expectWelcome() {
    await expect(this.page.locator('[data-testid="district-setup-welcome"]')).toBeVisible({ timeout: 15000 });
  }

  async start() {
    await this.page.locator('[data-testid="district-setup-next-0"]').click();
    await expect(this.page.locator('[data-testid="district-setup-school"]')).toBeVisible();
  }

  async createSchool(name: string, state?: string) {
    await this.page.locator('[data-testid="district-setup-school-name"]').fill(name);
    if (state) {
      await this.page.locator('[data-testid="district-setup-school-state"]').fill(state);
    }
    await this.page.locator('[data-testid="district-setup-school-submit"]').click();
    await expect(this.page.locator('[data-testid="district-setup-school-created"]')).toBeVisible({ timeout: 15000 });
  }

  async toStaffStep() {
    await this.page.locator('[data-testid="district-setup-next-1"]').click();
    await expect(this.page.locator('[data-testid="district-setup-staff"]')).toBeVisible();
  }

  async inviteTeacher(email: string) {
    await this.page.locator('[data-testid="district-staff-invite-email"]').fill(email);
    await this.page.locator('[data-testid="district-staff-invite-role"]').selectOption('3'); // Teacher
    // DistrictAdmin must pick the target school (the single one created in the
    // prior step). selectOption by index 1 = first real school (index 0 is the
    // "Select a school" placeholder).
    await this.page
      .locator('[data-testid="district-staff-invite-school"]')
      .selectOption({ index: 1 });
    await this.page.locator('[data-testid="district-staff-invite-submit"]').click();
  }

  async expectInviteUrl() {
    await expect(this.page.locator('[data-testid="staff-invite-url"]')).toBeVisible({ timeout: 15000 });
    return this.page.locator('[data-testid="staff-invite-url"]').inputValue();
  }

  async toDone() {
    await this.page.locator('[data-testid="district-setup-next-2"]').click();
    await expect(this.page.locator('[data-testid="district-setup-done"]')).toBeVisible();
  }

  async finish() {
    await this.page.locator('[data-testid="district-setup-finish"]').click();
    await this.page.waitForURL(/\/educator$/, { timeout: 15000 });
  }
}
