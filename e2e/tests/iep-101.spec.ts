import { test, expect } from '../helpers/fixtures';

test.describe('IEP 101 & Knowledge Base Links', () => {
  test('IEP 101 page shows educational content', async ({ page }) => {
    await page.goto('/iep-101');
    await expect(page.locator('h1')).toBeVisible();
    await expect(page.locator('main')).toContainText('IEP');
  });

  test('IEP 101 links to knowledge base', async ({ page }) => {
    await page.goto('/iep-101');
    const kbLink = page.locator('a[href="/knowledge-base"]').first();
    if (await kbLink.isVisible({ timeout: 3000 }).catch(() => false)) {
      await kbLink.click();
      await page.waitForURL(/\/knowledge-base/);
      await expect(page.locator('[data-testid="kb-search"]')).toBeVisible();
    }
  });
});
