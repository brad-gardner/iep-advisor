import { test, expect } from '../helpers/fixtures';

test.describe('Child Management', () => {
  test('create child with all fields', async ({ page }) => {
    await page.goto('/children/new');

    await page.fill('input[id="first-name"]', 'Emma');
    await page.fill('input[id="last-name"]', 'TestChild');
    await page.fill('input[type="date"]', '2015-06-15');

    // Use placeholder-based selectors for the remaining fields
    await page.locator('input[placeholder*="3rd"]').fill('3rd');
    await page.locator('input[placeholder*="Autism"]').fill('Autism');
    await page.locator('input[id="school-district"]').fill('Test District');

    await page.click('button:has-text("Create Profile")');

    // Should redirect to children list
    await page.waitForURL('/children');
    await expect(page.locator('text=Emma')).toBeVisible();
  });

  test('edit child profile', async ({ page }) => {
    await page.goto('/children');

    // Click on a child
    await page.locator('a:has-text("Emma")').first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Click edit
    await page.click('button:has-text("Edit")');

    // Change grade
    await page.locator('input[placeholder*="3rd"]').fill('4th');
    await page.click('button:has-text("Save")');

    // Verify updated
    await expect(page.locator('text=4th')).toBeVisible();
  });
});
