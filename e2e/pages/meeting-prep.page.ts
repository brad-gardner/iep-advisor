import { Page, expect } from '@playwright/test';

export class MeetingPrepPage {
  constructor(private page: Page) {}

  async clickPrepForMeeting() {
    await this.page.locator('[data-testid="generate-meeting-prep"]').click();
  }

  async expectMeetingPrepVisible() {
    await expect(this.page.locator('[data-testid="meeting-prep-progress"]')).toBeVisible();
  }
}
