import { Page, expect } from '@playwright/test';

export class ChildrenPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/children');
  }

  async gotoNew() {
    await this.page.goto('/children/new');
  }

  async gotoChild(childId: number) {
    await this.page.goto(`/children/${childId}`);
  }

  async clickFirstChild() {
    await this.page.locator('main a[href*="/children/"]').first().click();
    await this.page.waitForURL(/\/children\/\d+/);
  }

  async clickChildByName(name: string) {
    await this.page.locator(`main a:has([data-testid="child-card"]:has-text("${name}"))`).first().click();
    await this.page.waitForURL(/\/children\/\d+/);
  }

  async updateGradeLevel(grade: string) {
    await this.page.locator('[data-testid="child-grade-level"]').clear();
    await this.page.locator('[data-testid="child-grade-level"]').fill(grade);
  }

  async fillChildForm(data: {
    firstName: string;
    lastName?: string;
    dateOfBirth?: string;
    gradeLevel?: string;
    disabilityCategory?: string;
    schoolDistrict?: string;
  }) {
    await this.page.locator('[data-testid="child-first-name"]').fill(data.firstName);
    if (data.lastName) await this.page.locator('[data-testid="child-last-name"]').fill(data.lastName);
    if (data.dateOfBirth) await this.page.locator('[data-testid="child-date-of-birth"]').fill(data.dateOfBirth);
    if (data.gradeLevel) await this.page.locator('[data-testid="child-grade-level"]').fill(data.gradeLevel);
    if (data.disabilityCategory) await this.page.locator('[data-testid="child-disability-category"]').fill(data.disabilityCategory);
    if (data.schoolDistrict) await this.page.locator('[data-testid="child-school-district"]').fill(data.schoolDistrict);
  }

  async submitCreateForm() {
    await this.page.locator('[data-testid="child-form-submit"]').click();
  }

  async clickEdit() {
    await this.page.locator('[data-testid="child-edit-button"]').click();
  }

  async submitEditForm() {
    await this.page.locator('[data-testid="child-form-submit"]').click();
  }

  async clickNewIep() {
    await this.page.locator('[data-testid="new-iep-button"]').click();
  }

  async expectOnChildrenList() {
    await this.page.waitForURL('/children');
  }

  async clickRemove() {
    this.page.on('dialog', (dialog) => dialog.accept());
    await this.page.locator('[data-testid="child-remove-button"]').click();
  }

  async expectChildVisible(name: string) {
    await expect(this.page.getByText(name).first()).toBeVisible();
  }

  async expectChildNotVisible(name: string) {
    await expect(this.page.getByText(name)).not.toBeVisible({ timeout: 5000 });
  }
}
