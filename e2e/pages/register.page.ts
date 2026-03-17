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
    await this.page.getByLabel('Invite Code').fill(data.inviteCode);
    await this.page.getByLabel('First Name').fill(data.firstName);
    await this.page.getByLabel('Last Name').fill(data.lastName);
    await this.page.getByLabel('Email').fill(data.email);
    await this.page.getByLabel('Password', { exact: true }).fill(data.password);
    await this.page.getByLabel('Confirm Password').fill(data.password);
  }

  async submit() {
    await this.page.getByRole('button', { name: 'Create Account' }).click();
  }

  async expectRedirectToLogin() {
    await this.page.waitForURL(/\/login/, { timeout: 15000 });
  }

  async expectStayedOnRegister() {
    await this.page.waitForTimeout(3000);
    await expect(this.page).toHaveURL(/\/register/);
  }
}
