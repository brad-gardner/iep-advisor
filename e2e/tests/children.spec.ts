import { test, expect } from '../helpers/fixtures';

test.describe('Child Management', () => {
  test('create child with all fields', async ({ page }) => {
    await page.goto('/children/new');

    await page.getByLabel('First Name *').fill('Emma');
    await page.getByLabel('Last Name').fill('TestChild');
    await page.getByLabel('Date of Birth').fill('2015-06-15');
    await page.getByLabel('Grade Level').fill('3rd');
    await page.getByLabel('Disability Category').fill('Autism');
    await page.getByLabel('School District').fill('Test District');

    await page.getByRole('button', { name: 'Create Profile' }).click();

    // Should redirect to children list
    await page.waitForURL('/children');
    await expect(page.locator('text=Emma')).toBeVisible();
  });

  test('edit child profile', async ({ page }) => {
    // First create a child to edit
    await page.goto('/children/new');
    await page.getByLabel('First Name *').fill('EditTest');
    await page.getByLabel('Last Name').fill('Child');
    await page.getByLabel('Grade Level').fill('3rd');
    await page.getByRole('button', { name: 'Create Profile' }).click();
    await page.waitForURL('/children');

    // Click on the child we just created (scope to main content area to avoid sidebar)
    await page.locator('main a[href*="/children/"]').filter({ hasText: 'EditTest' }).first().click();
    await page.waitForURL(/\/children\/\d+/);

    // Click edit
    await page.getByRole('button', { name: 'Edit' }).click();

    // Change grade
    await page.getByLabel('Grade Level').fill('4th');
    await page.getByRole('button', { name: 'Save Changes' }).click();

    // Verify updated
    await expect(page.locator('text=4th')).toBeVisible();
  });
});
