import { test, expect } from '../helpers/fixtures';
import { ProfilePage } from '../pages/profile.page';

test.describe('Profile', () => {
  test('update name and state', async ({ page }) => {
    const profile = new ProfilePage(page);

    await profile.goto();
    await profile.updateFirstName('UpdatedName');
    await profile.selectState('OH');
    await profile.save();
    await profile.expectSaveSuccess();
  });

  test('email field is disabled', async ({ page }) => {
    await page.goto('/profile');
    const emailInput = page.locator('[data-testid="profile-email"]');
    await expect(emailInput).toBeDisabled();
  });
});
