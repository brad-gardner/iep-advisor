import { Page, expect } from '@playwright/test';

export class IepDocumentsPage {
  constructor(private page: Page) {}

  async clickNewIep() {
    await this.page.click('button:has-text("New IEP")');
  }

  async createIep(date: string, meetingType: string) {
    await this.clickNewIep();
    await this.page.fill('input[type="date"]', date);
    await this.page.locator('select').selectOption(meetingType);
    await this.page.click('button:has-text("Create IEP")');
  }

  async expectIepVisible(meetingTypeLabel: string) {
    // Use span selector to target badge, not the hidden select option
    await expect(this.page.locator(`span:has-text("${meetingTypeLabel}")`).first()).toBeVisible();
  }

  async expectFileInputAttached() {
    await expect(this.page.locator('input[type="file"]').first()).toBeAttached();
  }
}
