import { Page, expect } from '@playwright/test';

export class ChildrenPage {
  constructor(private page: Page) {}

  async goto() {
    await this.page.goto('/children');
  }

  async gotoNew() {
    await this.page.goto('/children/new');
  }

  async clickFirstChild() {
    await this.page.locator('a[href*="/children/"]').first().click();
    await this.page.waitForURL(/\/children\/\d+/);
  }

  async clickChildByName(name: string) {
    await this.page.locator(`a:has-text("${name}")`).first().click();
    await this.page.waitForURL(/\/children\/\d+/);
  }

  async fillChildForm(data: {
    firstName: string;
    lastName?: string;
    dateOfBirth?: string;
    gradeLevel?: string;
    disabilityCategory?: string;
    schoolDistrict?: string;
  }) {
    await this.page.fill('input[id="first-name"]', data.firstName);
    if (data.lastName) await this.page.fill('input[id="last-name"]', data.lastName);
    if (data.dateOfBirth) await this.page.fill('input[type="date"]', data.dateOfBirth);
    if (data.gradeLevel) await this.page.locator('input[placeholder*="3rd"]').fill(data.gradeLevel);
    if (data.disabilityCategory) await this.page.locator('input[placeholder*="Autism"]').fill(data.disabilityCategory);
    if (data.schoolDistrict) await this.page.locator('input[id="school-district"]').fill(data.schoolDistrict);
  }

  async submitCreateForm() {
    await this.page.click('button:has-text("Create Profile")');
  }

  async clickEdit() {
    await this.page.click('button:has-text("Edit")');
  }

  async updateGradeLevel(grade: string) {
    await this.page.locator('input[placeholder*="3rd"]').fill(grade);
  }

  async submitEditForm() {
    await this.page.click('button:has-text("Save")');
  }

  async expectOnChildrenList() {
    await this.page.waitForURL('/children');
  }

  async expectChildVisible(name: string) {
    await expect(this.page.locator(`text=${name}`)).toBeVisible();
  }
}
