import { test, expect } from '../helpers/fixtures';

test.describe('Knowledge Base', () => {
  test('search returns results', async ({ loggedInPage: page }) => {
    await page.goto('/knowledge-base');

    // Type a search
    await page.locator('input[placeholder*="Search"]').fill('FAPE');

    // Wait for debounced search
    await page.waitForTimeout(500);

    // Should see FAPE-related entries
    await expect(page.locator('text=Free Appropriate Public Education')).toBeVisible();
  });

  test('filter by category', async ({ loggedInPage: page }) => {
    await page.goto('/knowledge-base');

    // Click the Glossary tab
    await page.click('button:has-text("Glossary")');

    // Should show glossary entries
    await expect(page.locator('text=FAPE')).toBeVisible();
  });
});
