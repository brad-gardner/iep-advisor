import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';
import { ChildrenPage } from '../pages/children.page';
import { AdvocacyGoalsPage } from '../pages/advocacy-goals.page';

test.describe('Advocacy Goals', () => {
  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'GoalTestChild', 'E2E');
    childId = child.id;
  });

  test('add advocacy goal with category', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.gotoChild(childId);

    await goals.addGoal(
      'Improve reading fluency to grade level by the end of the school year',
      'academic',
    );

    await goals.expectGoalVisible('Improve reading fluency');
  });

  test('edit existing goal', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.gotoChild(childId);

    await goals.editGoal('Updated reading fluency goal with new target of 120 wpm');

    await goals.expectGoalVisible('Updated reading fluency');
  });

  test('delete goal with confirmation', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.gotoChild(childId);

    await goals.deleteGoal();
  });
});
