import { test, expect } from '../helpers/fixtures';

test.describe('Subscription & Usage', () => {
  test('dashboard shows subscription status card', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.locator('[data-testid="dashboard-subscription"]')).toBeVisible();
    await expect(page.locator('[data-testid="subscription-status"]')).toBeVisible();
  });

  test('subscription page loads', async ({ page }) => {
    await page.goto('/subscription');
    await expect(page.locator('[data-testid="subscription-status"]')).toBeVisible();
  });
});
