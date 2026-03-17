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
    await expect(this.page.locator(`text=${meetingTypeLabel}`)).toBeVisible();
  }

  async expectFileInputAttached() {
    await expect(this.page.locator('input[type="file"]').first()).toBeAttached();
  }
}
