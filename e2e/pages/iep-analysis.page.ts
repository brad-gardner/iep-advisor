import { Page, expect } from '@playwright/test';

export class IepAnalysisPage {
  constructor(private page: Page) {}

  /** Opens the first IEP card. Returns its child and document ids, or null when the list is empty. */
  async navigateToFirstIep(): Promise<{ childId: string; iepId: string } | null> {
    const viewLink = this.page.locator('[data-testid="iep-document-card"] a').first();
    if (!(await viewLink.isVisible())) return null;
    await viewLink.click();
    await this.page.waitForURL(/\/children\/\d+\/ieps\/\d+/);
    const [, childId, iepId] = this.page.url().match(/\/children\/(\d+)\/ieps\/(\d+)/) ?? [];
    return { childId, iepId };
  }

  async clickAnalysisTab() {
    await this.page.locator('[data-testid="tab-analysis"]').click();
  }

  /**
   * Starts a single-IEP analysis run when the IEP has none (or a stale one) and waits for it to
   * finish. A full analysis takes several minutes (one Claude call per document), so this polls by
   * reloading for up to ~12 minutes.
   */
  async triggerAnalysisIfNeeded() {
    const analyze = this.page.locator('[data-testid="analyze-button"], [data-testid="reanalyze-button"]').first();
    if (!(await analyze.isVisible())) return;
    await analyze.click();

    const overview = this.page.locator('[data-testid="analysis-nav-overview"]');
    for (let i = 0; i < 72; i++) {
      if (await overview.isVisible()) return;
      await this.page.waitForTimeout(10_000);
      await this.page.reload();
      await this.clickAnalysisTab();
    }
  }

  async expectOverviewVisible() {
    await expect(this.page.locator('[data-testid="analysis-nav-overview"]')).toBeVisible();
  }

  /** The child-level Analysis tab lists the run, and its IEP source links back to this IEP. */
  async expectRunOnChildTimeline(childId: string, iepId: string) {
    await this.page.goto(`/children/${childId}/analysis`);
    await expect(this.page.locator('[data-testid="analysis-run-history"]')).toBeVisible();
    await expect(this.page.locator('[data-testid="analysis-run-detail"]')).toBeVisible();
    await expect(this.page).toHaveURL(/[?&]run=\d+/);
    await expect(
      this.page.locator(`[data-testid="analysis-run-detail"] a[href$="/children/${childId}/ieps/${iepId}"]`).first(),
    ).toBeVisible();
  }
}
