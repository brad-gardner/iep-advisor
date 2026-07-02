import { Page, expect } from '@playwright/test';

// The two-path /register screen: a chooser (parent | district) followed by the
// selected form. Parent and district fields live under different testid prefixes.
export class DistrictRegisterPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/register');
  }

  async expectChooserVisible() {
    await expect(this.page.locator('[data-testid="register-path-chooser"]')).toBeVisible();
    await expect(this.page.locator('[data-testid="register-path-parent"]')).toBeVisible();
    await expect(this.page.locator('[data-testid="register-path-district"]')).toBeVisible();
  }

  async chooseDistrict() {
    await this.page.locator('[data-testid="register-path-district"]').click();
    await expect(this.page.locator('[data-testid="register-district-form"]')).toBeVisible();
  }

  async chooseParent() {
    await this.page.locator('[data-testid="register-path-parent"]').click();
    await expect(this.page.locator('[data-testid="register-form"]')).toBeVisible();
  }

  async fillDistrictForm(data: {
    firstName: string;
    lastName: string;
    email: string;
    districtName: string;
    state?: string;
    password: string;
  }) {
    await this.page.locator('[data-testid="register-district-first-name"]').fill(data.firstName);
    await this.page.locator('[data-testid="register-district-last-name"]').fill(data.lastName);
    await this.page.locator('[data-testid="register-district-email"]').fill(data.email);
    await this.page.locator('[data-testid="register-district-name"]').fill(data.districtName);
    if (data.state) {
      await this.page.locator('[data-testid="register-district-state"]').fill(data.state);
    }
    await this.page.locator('[data-testid="register-district-password"]').fill(data.password);
    await this.page.locator('[data-testid="register-district-confirm-password"]').fill(data.password);
  }

  async submitDistrict() {
    await this.page.locator('[data-testid="register-district-submit"]').click();
  }
}
