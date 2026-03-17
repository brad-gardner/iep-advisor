import { Page } from '@playwright/test';

export class Sidebar {
  constructor(private page: Page) {}

  async signOut() {
    // Target the visible Sign Out button (desktop sidebar, not hidden mobile one)
    await this.page.getByRole('button', { name: /sign out/i }).first().click({ force: true });
  }
}
