import { test, expect } from '../helpers/fixtures';

test.describe('Profile', () => {
  test('update name and state', async ({ loggedInPage: page }) => {
    await page.goto('/profile');

    // Clear and update first name
    const firstNameInput = page.locator('input').nth(1); // Skip disabled email
    await firstNameInput.clear();
    await firstNameInput.fill('UpdatedName');

    // Select a state
    await page.locator('select#state').selectOption('OH');

    await page.click('button:has-text("Save")');

    await expect(page.locator('text=Profile updated')).toBeVisible({ timeout: 5000 });
  });
});
