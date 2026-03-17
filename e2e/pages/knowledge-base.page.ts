import { Page, expect } from '@playwright/test';

export class KnowledgeBasePage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/knowledge-base');
  }

  async search(query: string) {
    await this.page.locator('input[placeholder*="Search"]').fill(query);
    await this.page.waitForTimeout(500); // debounce
  }

  async selectCategory(name: string) {
    await this.page.click(`button:has-text("${name}")`);
  }

  async expectResultVisible(text: string) {
    await expect(this.page.getByText(text).first()).toBeVisible();
  }
}
