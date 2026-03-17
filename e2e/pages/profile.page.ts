import { Page, expect } from '@playwright/test';

export class ProfilePage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/profile');
  }

  async updateFirstName(name: string) {
    await this.page.locator('[data-testid="profile-first-name"]').clear();
    await this.page.locator('[data-testid="profile-first-name"]').fill(name);
  }

  async selectState(stateCode: string) {
    await this.page.locator('[data-testid="profile-state"]').selectOption(stateCode);
  }

  async save() {
    await this.page.locator('[data-testid="profile-save"]').click();
  }

  async expectSaveSuccess() {
    await expect(this.page.locator('text=Profile updated')).toBeVisible({ timeout: 5000 });
  }
}
