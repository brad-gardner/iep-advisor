import { Page, expect } from '@playwright/test';

export class IepDocumentsPage {
  constructor(private page: Page) {}

  async clickNewIep() {
    await this.page.locator('[data-testid="new-iep-button"]').click();
  }

  async createIep(date: string, meetingType: string) {
    await this.clickNewIep();
    await this.page.locator('[data-testid="iep-meeting-date"]').fill(date);
    await this.page.locator('[data-testid="iep-meeting-type"]').selectOption(meetingType);
    await this.page.locator('[data-testid="iep-create-submit"]').click();
  }

  async expectIepVisible(meetingTypeLabel: string) {
    await expect(
      this.page.locator(`[data-testid="iep-document-card"]:has-text("${meetingTypeLabel}")`).first()
    ).toBeVisible();
  }

  async expectFileInputAttached() {
    await expect(this.page.locator('[data-testid="iep-file-input"]').first()).toBeAttached();
  }
}
