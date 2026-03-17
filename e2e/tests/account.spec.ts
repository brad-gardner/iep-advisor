import { test, expect } from '../helpers/fixtures';

test.describe('Account Management', () => {
  test('export data button visible on profile page', async ({ page }) => {
    await page.goto('/profile');

    await expect(page.locator('[data-testid="account-section"]')).toBeVisible();
    await expect(page.locator('[data-testid="export-data"]')).toBeVisible();
  });

  test('export data triggers download', async ({ page }) => {
    await page.goto('/profile');

    // Listen for download
    const downloadPromise = page.waitForEvent('download', { timeout: 10000 });
    await page.locator('[data-testid="export-data"]').click();
    const download = await downloadPromise;

    // Verify download filename
    expect(download.suggestedFilename()).toMatch(/iep-assistant-data-export.*\.json/);
  });

  test('delete account button visible and shows confirmation', async ({ page }) => {
    await page.goto('/profile');

    await expect(page.locator('[data-testid="delete-account"]')).toBeVisible();

    // Click delete — should show confirmation form
    await page.locator('[data-testid="delete-account"]').click();
    await expect(page.locator('[data-testid="delete-account-password"]')).toBeVisible();
    await expect(page.locator('[data-testid="confirm-delete-account"]')).toBeVisible();
    await expect(page.locator('[data-testid="cancel-delete-account"]')).toBeVisible();
  });

  test('cancel delete account hides confirmation', async ({ page }) => {
    await page.goto('/profile');

    await page.locator('[data-testid="delete-account"]').click();
    await expect(page.locator('[data-testid="confirm-delete-account"]')).toBeVisible();

    await page.locator('[data-testid="cancel-delete-account"]').click();
    await expect(page.locator('[data-testid="confirm-delete-account"]')).not.toBeVisible();
    await expect(page.locator('[data-testid="delete-account"]')).toBeVisible();
  });
});
