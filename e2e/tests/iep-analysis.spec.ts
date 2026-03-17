import { test, expect } from '../helpers/fixtures';

test.describe('IEP Analysis', () => {
  test.setTimeout(120_000); // 2 minutes for AI analysis

  test('trigger analysis and view results', async ({ page }) => {
    // This test requires a parsed IEP to exist
    // Navigate to an IEP viewer
    await page.goto('/children');
    const iepLink = page.locator('a:has-text("View")').first();

    if (await iepLink.isVisible()) {
      await iepLink.click();
      await page.waitForURL(/\/ieps\/\d+/);

      // Click Analysis tab
      await page.click('button:has-text("Analysis")');

      // If analysis doesn't exist, trigger it
      const analyzeButton = page.locator('button:has-text("Analyze")');
      if (await analyzeButton.isVisible()) {
        await analyzeButton.click();

        // Poll for completion (up to 90 seconds)
        for (let i = 0; i < 18; i++) {
          await page.waitForTimeout(5000);
          await page.reload();
          await page.click('button:has-text("Analysis")');

          const overview = page.locator('h2:has-text("Overview")');
          if (await overview.isVisible()) break;
        }
      }

      // Verify analysis results are displayed
      await expect(page.locator('h2:has-text("Overview")')).toBeVisible();
    } else {
      test.skip(true, 'No parsed IEP available for analysis test');
    }
  });
});
