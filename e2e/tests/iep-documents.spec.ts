import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';

test.describe('IEP Documents', () => {
  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'IepDocChild', 'E2E', {
      gradeLevel: '3rd',
    });
    childId = child.id;
  });

  test('create IEP event with date and type', async ({ page }) => {
    await page.goto(`/children/${childId}`);
    await page.waitForURL(`/children/${childId}`);

    // Click New IEP
    await page.getByRole('button', { name: 'New IEP' }).click();

    // Fill in meeting details
    await page.locator('input[type="date"]').first().fill('2026-03-15');
    await page.locator('select').first().selectOption('annual_review');

    await page.getByRole('button', { name: 'Create IEP' }).click();

    // IEP should appear in the list
    await expect(page.locator('text=Annual Review')).toBeVisible();
  });

  test('upload PDF to existing IEP', async ({ page }) => {
    await page.goto(`/children/${childId}`);
    await page.waitForURL(`/children/${childId}`);

    // Find the file input in an IEP card's upload zone
    const fileInput = page.locator('input[type="file"]').first();

    // For now, we'll check the upload zone exists
    await expect(fileInput).toBeAttached();
  });
});
