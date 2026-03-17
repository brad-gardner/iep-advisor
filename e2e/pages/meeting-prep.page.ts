import { Page, expect } from '@playwright/test';

export class MeetingPrepPage {
  constructor(private page: Page) {}

  async clickPrepForMeeting() {
    return this.page.locator('button:has-text("Prep for Meeting")');
  }

  async expectMeetingPrepVisible() {
    await expect(this.page.locator('text=Meeting Prep').first()).toBeVisible();
  }
}
