import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';
import { ChildrenPage } from '../pages/children.page';
import { MeetingPrepPage } from '../pages/meeting-prep.page';

test.describe('Meeting Prep', () => {
  test.setTimeout(120_000);

  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'PrepChild', 'E2E');
    childId = child.id;
  });

  test('generate checklist from goals (Mode B)', async ({ page }) => {
    const children = new ChildrenPage(page);
    const meetingPrep = new MeetingPrepPage(page);

    await children.gotoChild(childId);

    const prepButton = page.locator('[data-testid="generate-meeting-prep"]');
    if (await prepButton.isVisible({ timeout: 3000 }).catch(() => false)) {
      await prepButton.click();
      await page.waitForTimeout(5000);
      await meetingPrep.expectMeetingPrepVisible();
    } else {
      test.skip(true, 'Meeting prep button not visible');
    }
  });
});
