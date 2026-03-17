import { Page, expect } from '@playwright/test';

export class LoginPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/login');
  }

  async login(email: string, password: string) {
    await this.page.getByLabel('Email').fill(email);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Sign In' }).click();
  }

  async expectRedirectToDashboard() {
    await this.page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });
  }

  async expectStayedOnLogin() {
    await this.page.waitForTimeout(3000);
    await expect(this.page).toHaveURL(/\/login/);
  }

  async expectSuccessMessage() {
    await expect(this.page.getByText(/registration successful/i).first()).toBeVisible({ timeout: 5000 });
  }
}
