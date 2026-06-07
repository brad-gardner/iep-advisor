import { Page, expect } from '@playwright/test';

export class RegisterPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/register');
  }

  // The /register screen now leads with a parent|district chooser. Parent fields
  // only render after the parent path is selected. Each fillForm() picks it.
  async chooseParent() {
    await this.page.locator('[data-testid="register-path-parent"]').click();
    await this.page.locator('[data-testid="register-form"]').waitFor({ state: 'visible' });
  }

  async fillForm(data: {
    inviteCode: string;
    firstName: string;
    lastName: string;
    email: string;
    password: string;
  }) {
    // Ensure the parent form is shown (idempotent — clicking again is harmless).
    if (await this.page.locator('[data-testid="register-form"]').count() === 0) {
      await this.chooseParent();
    }
    await this.page.locator('[data-testid="register-invite-code"]').fill(data.inviteCode);
    await this.page.locator('[data-testid="register-first-name"]').fill(data.firstName);
    await this.page.locator('[data-testid="register-last-name"]').fill(data.lastName);
    await this.page.locator('[data-testid="register-email"]').fill(data.email);
    await this.page.locator('[data-testid="register-password"]').fill(data.password);
    await this.page.locator('[data-testid="register-confirm-password"]').fill(data.password);
  }

  async submit() {
    await this.page.locator('[data-testid="register-submit"]').click();
  }

  async expectRedirectToLogin() {
    await this.page.waitForURL(/\/login/, { timeout: 15000 });
  }

  async expectStayedOnRegister() {
    await this.page.waitForTimeout(3000);
    await expect(this.page).toHaveURL(/\/register/);
  }
}
