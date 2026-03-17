import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';

test.describe('Advocacy Goals', () => {
  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'GoalTestChild', 'E2E', {
      gradeLevel: '5th',
      disabilityCategory: 'Autism',
    });
    childId = child.id;
  });

  test('add advocacy goal with category', async ({ page }) => {
    await page.goto(`/children/${childId}`);
    await page.waitForURL(`/children/${childId}`);

    // Find the advocacy goals section and add a goal
    await page.getByRole('button', { name: /Add|Add Your First Goal/ }).first().click();

    await page.getByLabel('Advocacy Goal').fill('Improve reading fluency to grade level by the end of the school year');
    await page.getByLabel('Category (optional)').selectOption('academic');
    await page.getByRole('button', { name: 'Add Goal' }).click();

    await expect(page.locator('text=Improve reading fluency')).toBeVisible();
  });

  test('edit existing goal', async ({ page }) => {
    await page.goto(`/children/${childId}`);
    await page.waitForURL(`/children/${childId}`);

    // Click edit on first goal
    await page.locator('button[aria-label="Edit goal"]').first().click();

    await page.locator('textarea').first().fill('Updated reading fluency goal with new target of 120 wpm');
    await page.getByRole('button', { name: 'Save Changes' }).click();

    await expect(page.locator('text=Updated reading fluency')).toBeVisible();
  });

  test('delete goal with confirmation', async ({ page }) => {
    await page.goto(`/children/${childId}`);
    await page.waitForURL(`/children/${childId}`);

    // Accept the confirmation dialog
    page.on('dialog', dialog => dialog.accept());

    await page.locator('button[aria-label="Delete goal"]').first().click();

    // Goal should be removed (or empty state shown)
    await page.waitForTimeout(500);
  });
});
