import { Page, expect } from '@playwright/test';

export class LoginPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/login');
  }

  async login(email: string, password: string) {
    await this.page.locator('[data-testid="login-email"]').fill(email);
    await this.page.locator('[data-testid="login-password"]').fill(password);
    await this.page.locator('[data-testid="login-submit"]').click();
  }

  async expectRedirectToDashboard() {
    await this.page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });
  }

  async expectStayedOnLogin() {
    await this.page.waitForTimeout(3000);
    await expect(this.page).toHaveURL(/\/login/);
  }

  async expectSuccessMessage() {
    await expect(this.page.locator('[data-testid="login-success-message"]')).toBeVisible({ timeout: 5000 });
  }

  async expectError() {
    await expect(this.page.locator('[data-testid="login-error"]')).toBeVisible({ timeout: 5000 });
  }
}
