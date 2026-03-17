import { Page, expect } from '@playwright/test';

export class ProfilePage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/profile');
  }

  async updateFirstName(name: string) {
    const firstNameInput = this.page.locator('input').nth(1); // Skip disabled email
    await firstNameInput.clear();
    await firstNameInput.fill(name);
  }

  async selectState(stateCode: string) {
    await this.page.locator('select#state').selectOption(stateCode);
  }

  async save() {
    await this.page.click('button:has-text("Save")');
  }

  async expectSaveSuccess() {
    await expect(this.page.locator('text=Profile updated')).toBeVisible({ timeout: 5000 });
  }
}
