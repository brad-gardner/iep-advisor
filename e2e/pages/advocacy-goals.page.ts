import { Page, expect } from '@playwright/test';

export class AdvocacyGoalsPage {
  constructor(private page: Page) {}

  async addGoal(text: string, category?: string) {
    await this.page.locator('[data-testid="add-goal-button"]').click();
    await this.page.locator('[data-testid="goal-text-input"]').fill(text);
    if (category) {
      await this.page.locator('[data-testid="goal-category-select"]').selectOption(category);
    }
    await this.page.locator('[data-testid="goal-form-submit"]').click();
  }

  async editGoal(newText: string) {
    await this.page.locator('[data-testid="goal-edit"]').first().click();
    await this.page.locator('[data-testid="goal-text-input"]').fill(newText);
    await this.page.locator('[data-testid="goal-form-submit"]').click();
  }

  async deleteGoal() {
    // The native confirm() was replaced by a ConfirmDialog overlay.
    await this.page.locator('[data-testid="goal-delete"]').first().click();
    await this.page.locator('[data-testid="goal-delete-dialog-confirm"]').click();
    await this.page.waitForTimeout(500);
  }

  async expectGoalVisible(text: string) {
    await expect(this.page.locator(`[data-testid="goal-card"]:has-text("${text}")`)).toBeVisible();
  }
}
