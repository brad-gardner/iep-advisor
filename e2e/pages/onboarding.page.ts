import { Page, expect } from '@playwright/test';

export class OnboardingPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/onboarding');
  }

  async expectProgressStep(stepNumber: number) {
    await expect(
      this.page.locator('[data-testid="onboarding-progress"]')
    ).toContainText(`Step ${stepNumber} of 4`);
  }

  async expectWelcomeStep() {
    await expect(this.page.locator('[data-testid="onboarding-step"]')).toContainText('Welcome to IEP Advisor');
  }

  async clickStart() {
    await this.page.locator('[data-testid="onboarding-start"]').click();
  }

  async expectStateStep() {
    await expect(this.page.locator('[data-testid="onboarding-step"]')).toContainText('Set Your State');
  }

  async selectState(stateCode: string) {
    await this.page.locator('[data-testid="profile-state"]').selectOption(stateCode);
  }

  async clickContinueState() {
    await this.page.locator('[data-testid="onboarding-continue-state"]').click();
  }

  async clickSkipState() {
    await this.page.locator('[data-testid="onboarding-skip-state"]').click();
  }

  async expectChildStep() {
    await expect(this.page.locator('[data-testid="onboarding-step"]')).toContainText('Add Your First Child');
  }

  async fillChildName(name: string) {
    await this.page.locator('[data-testid="child-first-name"]').fill(name);
  }

  async submitChild() {
    await this.page.locator('[data-testid="child-form-submit"]').click();
  }

  async clickSkipChild() {
    await this.page.locator('[data-testid="onboarding-skip-child"]').click();
  }

  async expectNextSteps() {
    await expect(this.page.locator('[data-testid="onboarding-step"]')).toContainText("You're All Set!");
  }

  async clickFinish() {
    await this.page.locator('[data-testid="onboarding-finish"]').click();
  }

  async clickLearnAboutIeps() {
    await this.page.locator('[data-testid="onboarding-learn"]').click();
  }
}
