import { Page, expect } from '@playwright/test';

export class IepAnalysisPage {
  constructor(private page: Page) {}

  async clickViewLink() {
    return this.page.locator('a:has-text("View")').first();
  }

  async navigateToFirstIep() {
    const viewLink = await this.clickViewLink();
    if (await viewLink.isVisible()) {
      await viewLink.click();
      await this.page.waitForURL(/\/ieps\/\d+/);
      return true;
    }
    return false;
  }

  async clickAnalysisTab() {
    await this.page.click('button:has-text("Analysis")');
  }

  async triggerAnalysisIfNeeded() {
    const analyzeButton = this.page.locator('button:has-text("Analyze")');
    if (await analyzeButton.isVisible()) {
      await analyzeButton.click();

      for (let i = 0; i < 18; i++) {
        await this.page.waitForTimeout(5000);
        await this.page.reload();
        await this.clickAnalysisTab();

        const overview = this.page.locator('h2:has-text("Overview")');
        if (await overview.isVisible()) break;
      }
    }
  }

  async expectOverviewVisible() {
    await expect(this.page.locator('h2:has-text("Overview")')).toBeVisible();
  }
}
