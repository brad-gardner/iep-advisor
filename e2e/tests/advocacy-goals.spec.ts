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

  test('goal text under 10 chars shows validation error', async ({ page }) => {
    const children = new ChildrenPage(page);

    await children.gotoChild(childId);

    // Click add goal button
    await page.locator('[data-testid="add-goal-button"]').click();

    // Type short text and try to submit
    await page.locator('[data-testid="goal-text-input"]').fill('Too short');

    // Submit button should be disabled (< 10 chars)
    await expect(page.locator('[data-testid="goal-form-submit"]')).toBeDisabled();
  });

  test('reorder goals with up/down arrows', async ({ page }) => {
    const children = new ChildrenPage(page);
    const goals = new AdvocacyGoalsPage(page);

    await children.gotoChild(childId);

    // Add two goals for reordering
    await goals.addGoal('First goal for reorder testing purposes', 'academic');
    await goals.addGoal('Second goal for reorder testing purposes', 'behavioral');

    // Click move-down on first goal
    await page.locator('[data-testid="goal-move-down"]').first().click();
    await page.waitForTimeout(500);

    // Verify the goals are reordered — "Second" should now be first
    const firstGoal = page.locator('[data-testid="goal-card"]').first();
    await expect(firstGoal).toContainText('Second goal');
  });
});
