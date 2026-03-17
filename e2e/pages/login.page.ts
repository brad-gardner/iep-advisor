import { Page, expect } from '@playwright/test';

export class LoginPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/login');
  }

  async login(email: string, password: string) {
    await this.page.fill('input[type="email"]', email);
    await this.page.fill('input[type="password"]', password);
    await this.page.click('button[type="submit"]');
  }

  async expectRedirectToDashboard() {
    await this.page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });
  }

  async expectError(text: string) {
    await expect(this.page.locator(`text=${text}`)).toBeVisible({ timeout: 5000 });
  }

  async expectOnLoginPage() {
    await this.page.waitForURL('/login');
  }

  async expectSuccessMessage() {
    await expect(this.page.locator('text=Registration successful')).toBeVisible({ timeout: 5000 });
  }
}
