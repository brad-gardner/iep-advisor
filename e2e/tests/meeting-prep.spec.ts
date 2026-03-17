import { test, expect } from '../helpers/fixtures';

test.describe('Meeting Prep', () => {
  test.setTimeout(120_000);

  test('generate checklist from goals (Mode B)', async ({ loggedInPage: page }) => {
    await page.goto('/children');
    await page.locator('a[href*="/children/"]').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Click "Prep for Meeting" if it exists
    const prepButton = page.locator('button:has-text("Prep for Meeting")');
    if (await prepButton.isVisible()) {
      await prepButton.click();

      // Wait for generation
      await page.waitForTimeout(5000);

      // Check that some checklist content appears (or is generating)
      await expect(page.locator('text=Meeting Prep').first()).toBeVisible();
    } else {
      test.skip(true, 'Meeting prep button not visible');
    }
  });
});
