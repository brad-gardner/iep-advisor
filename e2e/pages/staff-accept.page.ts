import { Page, expect } from '@playwright/test';

// Public /staff/accept-invite?token=... page. Previews the invite (district /
// role / bound email) then registers the recipient and signs them in.
export class StaffAcceptPage {
  constructor(private page: Page) {}

  async goto(token: string) {
    await this.page.goto(`/staff/accept-invite?token=${encodeURIComponent(token)}`);
  }

  async expectPreview(opts: { district?: string; role?: string; email?: string }) {
    await expect(this.page.locator('[data-testid="staff-accept-form"]')).toBeVisible({ timeout: 15000 });
    const body = this.page.locator('body');
    if (opts.district) await expect(body).toContainText(opts.district);
    if (opts.role) await expect(body).toContainText(opts.role);
    if (opts.email) {
      await expect(this.page.locator('[data-testid="staff-accept-email"]')).toHaveValue(opts.email);
    }
  }

  async expectInvalid() {
    // Either the page never reaches the form (error Notice) — assert the form is absent.
    await expect(this.page.locator('[data-testid="staff-accept-form"]')).toHaveCount(0, { timeout: 15000 });
  }

  async fillAndSubmit(data: { firstName: string; lastName: string; password: string }) {
    await this.page.locator('[data-testid="staff-accept-first-name"]').fill(data.firstName);
    await this.page.locator('[data-testid="staff-accept-last-name"]').fill(data.lastName);
    await this.page.locator('[data-testid="staff-accept-password"]').fill(data.password);
    await this.page.locator('[data-testid="staff-accept-confirm-password"]').fill(data.password);
    await this.page.locator('[data-testid="staff-accept-submit"]').click();
  }

  async expectLandsOnEducator() {
    await this.page.waitForURL(/\/educator/, { timeout: 15000 });
    await expect(this.page.locator('[data-testid="educator-dashboard"]')).toBeVisible({ timeout: 15000 });
  }
}
