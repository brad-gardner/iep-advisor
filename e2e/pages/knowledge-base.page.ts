import { Page, expect } from '@playwright/test';

export class KnowledgeBasePage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/knowledge-base');
  }

  async search(query: string) {
    await this.page.locator('[data-testid="kb-search"]').fill(query);
    await this.page.waitForTimeout(500); // debounce
  }

  async selectCategory(name: string) {
    await this.page.locator(`[data-testid^="kb-tab-"]:has-text("${name}")`).click();
  }

  async expectResultVisible(text: string) {
    await expect(
      this.page.locator(`[data-testid="kb-results"] :has-text("${text}")`).first()
    ).toBeVisible();
  }
}
