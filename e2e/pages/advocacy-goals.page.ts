import { Page, expect } from '@playwright/test';

export class AdvocacyGoalsPage {
  constructor(private page: Page) {}

  async addGoal(text: string, category?: string) {
    await this.page.click('button:has-text("Add"), button:has-text("Add Your First Goal")');
    await this.page.locator('textarea').fill(text);
    if (category) {
      await this.page.locator('select').selectOption(category);
    }
    await this.page.click('button:has-text("Add Goal")');
  }

  async editGoal(newText: string) {
    await this.page.locator('button[aria-label="Edit goal"]').first().click();
    await this.page.locator('textarea').fill(newText);
    await this.page.click('button:has-text("Save Changes")');
  }

  async deleteGoal() {
    this.page.on('dialog', (dialog) => dialog.accept());
    await this.page.locator('button[aria-label="Delete goal"]').first().click();
    await this.page.waitForTimeout(500);
  }

  async expectGoalVisible(text: string) {
    await expect(this.page.locator(`text=${text}`)).toBeVisible();
  }
}
