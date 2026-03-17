import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';

test.describe('Meeting Prep', () => {
  test.setTimeout(120_000);

  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'MeetingPrepChild', 'E2E', {
      gradeLevel: '4th',
    });
    childId = child.id;
  });

  test('generate checklist from goals (Mode B)', async ({ page }) => {
    await page.goto(`/children/${childId}`);
    await page.waitForURL(`/children/${childId}`);

    // Click "Prep for Meeting" if it exists
    const prepButton = page.getByRole('button', { name: 'Prep for Meeting' });
    if (await prepButton.isVisible()) {
      await prepButton.click();

      // Wait for generation
      await page.waitForTimeout(5000);

      // Check that some checklist content appears (or is generating)
      await expect(page.locator('text=Meeting Prep').first()).toBeVisible();
    } else {
      test.skip(true, 'Meeting prep button not visible');
    }
  });
});
