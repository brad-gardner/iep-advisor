import { test, expect } from '../helpers/fixtures';

test.describe('Profile', () => {
  test('update name and state', async ({ page }) => {
    await page.goto('/profile');

    // Clear and update first name
    await page.getByLabel('First Name').clear();
    await page.getByLabel('First Name').fill('UpdatedName');

    // Select a state
    await page.locator('select#state').selectOption('OH');

    await page.getByRole('button', { name: 'Save Changes' }).click();

    await expect(page.locator('text=Profile updated')).toBeVisible({ timeout: 5000 });
  });
});
