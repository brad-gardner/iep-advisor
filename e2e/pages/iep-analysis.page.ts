import { Page, expect } from '@playwright/test';

export class IepAnalysisPage {
  constructor(private page: Page) {}

  async navigateToFirstIep() {
    const viewLink = this.page.locator('[data-testid="iep-document-card"] a').first();
    if (await viewLink.isVisible()) {
      await viewLink.click();
      await this.page.waitForURL(/\/ieps\/\d+/);
      return true;
    }
    return false;
  }

  async clickAnalysisTab() {
    await this.page.locator('[data-testid="tab-analysis"]').click();
  }

  async triggerAnalysisIfNeeded() {
    const analyzeButton = this.page.locator('[data-testid="analyze-button"]');
    if (await analyzeButton.isVisible()) {
      await analyzeButton.click();

      for (let i = 0; i < 18; i++) {
        await this.page.waitForTimeout(5000);
        await this.page.reload();
        await this.clickAnalysisTab();

        const overview = this.page.locator('[data-testid="analysis-nav-overview"]');
        if (await overview.isVisible()) break;
      }
    }
  }

  async expectOverviewVisible() {
    await expect(this.page.locator('[data-testid="analysis-nav-overview"]')).toBeVisible();
  }
}
