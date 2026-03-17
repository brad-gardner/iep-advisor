import { test, expect } from '../helpers/fixtures';

test.describe('IEP Documents', () => {
  test('create IEP event with date and type', async ({ page }) => {
    await page.goto('/children');
    await page.locator('a[href*="/children/"]').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Click New IEP
    await page.click('button:has-text("New IEP")');

    // Fill in meeting details
    await page.fill('input[type="date"]', '2026-03-15');
    await page.locator('select').selectOption('annual_review');

    await page.click('button:has-text("Create IEP")');

    // IEP should appear in the list
    await expect(page.locator('text=Annual Review')).toBeVisible();
  });

  test('upload PDF to existing IEP', async ({ page }) => {
    await page.goto('/children');
    await page.locator('a[href*="/children/"]').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Find the file input in an IEP card's upload zone
    const fileInput = page.locator('input[type="file"]').first();

    // For now, we'll check the upload zone exists
    await expect(fileInput).toBeAttached();
  });
});
