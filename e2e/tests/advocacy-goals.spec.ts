import { test, expect } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';
import { AdvocacyGoalsPage } from '../pages/advocacy-goals.page';

test.describe('Advocacy Goals', () => {
  test('add advocacy goal with category', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.goto();
    await children.clickFirstChild();

    await goals.addGoal(
      'Improve reading fluency to grade level by the end of the school year',
      'academic',
    );

    await goals.expectGoalVisible('Improve reading fluency');
  });

  test('edit existing goal', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.goto();
    await children.clickFirstChild();

    await goals.editGoal('Updated reading fluency goal with new target of 120 wpm');

    await goals.expectGoalVisible('Updated reading fluency');
  });

  test('delete goal with confirmation', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.goto();
    await children.clickFirstChild();

    await goals.deleteGoal();
  });
});
