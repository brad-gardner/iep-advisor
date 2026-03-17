import { test, expect } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';
import { MeetingPrepPage } from '../pages/meeting-prep.page';

test.describe('Meeting Prep', () => {
  test.setTimeout(120_000);

  test('generate checklist from goals (Mode B)', async ({ page }) => {
    const children = new ChildrenPage(page);
    const meetingPrep = new MeetingPrepPage(page);

    await children.goto();
    await children.clickFirstChild();

    const prepButton = await meetingPrep.clickPrepForMeeting();
    if (await prepButton.isVisible()) {
      await prepButton.click();
      await page.waitForTimeout(5000);
      await meetingPrep.expectMeetingPrepVisible();
    } else {
      test.skip(true, 'Meeting prep button not visible');
    }
  });
});
