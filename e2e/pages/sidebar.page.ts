import { Page } from '@playwright/test';

export class Sidebar {
  constructor(private page: Page) {}

  async signOut() {
    // Two sign-out buttons exist (desktop + mobile overlay); use JS click on whichever is in DOM
    await this.page.locator('[data-testid="sidebar-sign-out"]').first().dispatchEvent('click');
  }
}
