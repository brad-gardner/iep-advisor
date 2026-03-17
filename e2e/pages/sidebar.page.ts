import { Page } from '@playwright/test';

export class Sidebar {
  constructor(private page: Page) {}

  async signOut() {
    await this.page.click('text=Sign Out');
  }
}
