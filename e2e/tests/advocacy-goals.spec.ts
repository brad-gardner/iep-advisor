import { test, expect } from '../helpers/fixtures';

test.describe('Advocacy Goals', () => {
  test('add advocacy goal with category', async ({ page }) => {
    await page.goto('/children');
    await page.locator('a[href*="/children/"]').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Find the advocacy goals section and add a goal
    await page.click('button:has-text("Add"), button:has-text("Add Your First Goal")');

    await page.locator('textarea').fill('Improve reading fluency to grade level by the end of the school year');
    await page.locator('select').selectOption('academic');
    await page.click('button:has-text("Add Goal")');

    await expect(page.locator('text=Improve reading fluency')).toBeVisible();
  });

  test('edit existing goal', async ({ page }) => {
    await page.goto('/children');
    await page.locator('a[href*="/children/"]').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Click edit on first goal
    await page.locator('button[aria-label="Edit goal"]').first().click();

    await page.locator('textarea').fill('Updated reading fluency goal with new target of 120 wpm');
    await page.click('button:has-text("Save Changes")');

    await expect(page.locator('text=Updated reading fluency')).toBeVisible();
  });

  test('delete goal with confirmation', async ({ page }) => {
    await page.goto('/children');
    await page.locator('a[href*="/children/"]').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Accept the confirmation dialog
    page.on('dialog', dialog => dialog.accept());

    await page.locator('button[aria-label="Delete goal"]').first().click();

    // Goal should be removed (or empty state shown)
    await page.waitForTimeout(500);
  });
});
