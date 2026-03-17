import { Page, expect } from '@playwright/test';

export class RegisterPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/register');
  }

  async fillForm(data: {
    inviteCode: string;
    firstName: string;
    lastName: string;
    email: string;
    password: string;
  }) {
    await this.page.fill('input[name="inviteCode"]', data.inviteCode);
    await this.page.fill('input[name="firstName"]', data.firstName);
    await this.page.fill('input[name="lastName"]', data.lastName);
    await this.page.fill('input[name="email"]', data.email);
    await this.page.fill('input[name="password"]', data.password);
    await this.page.fill('input[name="confirmPassword"]', data.password);
  }

  async submit() {
    await this.page.click('button[type="submit"]');
  }

  async expectError(text: string) {
    await expect(this.page.locator(`text=${text}`)).toBeVisible({ timeout: 5000 });
  }
}
