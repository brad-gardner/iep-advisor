import { test, expect } from '../helpers/fixtures';

test.describe('Knowledge Base', () => {
  test('search returns results', async ({ page }) => {
    await page.goto('/knowledge-base');

    // Type a search
    await page.locator('input[placeholder*="Search"]').fill('FAPE');

    // Wait for debounced search
    await page.waitForTimeout(500);

    // Should see FAPE-related entries
    await expect(page.locator('text=Free Appropriate Public Education').first()).toBeVisible();
  });

  test('filter by category', async ({ page }) => {
    await page.goto('/knowledge-base');

    // Click the Glossary tab
    await page.getByRole('button', { name: 'Glossary' }).click();

    // Should show glossary entries
    await expect(page.locator('text=FAPE').first()).toBeVisible();
  });
});
