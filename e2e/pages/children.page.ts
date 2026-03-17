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
    // Scope to main content area to avoid matching sidebar "My Children" link
    await this.page.locator('main a[href*="/children/"]').first().click();
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
    await this.page.getByLabel('First Name *').fill(data.firstName);
    if (data.lastName) await this.page.getByLabel('Last Name').fill(data.lastName);
    if (data.dateOfBirth) await this.page.getByLabel('Date of Birth').fill(data.dateOfBirth);
    if (data.gradeLevel) await this.page.getByLabel('Grade Level').fill(data.gradeLevel);
    if (data.disabilityCategory) await this.page.getByLabel('Disability Category').fill(data.disabilityCategory);
    if (data.schoolDistrict) await this.page.getByLabel('School District').fill(data.schoolDistrict);
  }

  async submitCreateForm() {
    await this.page.getByRole('button', { name: 'Create Profile' }).click();
  }

  async clickEdit() {
    await this.page.getByRole('button', { name: 'Edit' }).click();
  }

  async submitEditForm() {
    await this.page.getByRole('button', { name: 'Save Changes' }).click();
  }

  async clickNewIep() {
    await this.page.getByRole('button', { name: 'New IEP' }).click();
  }

  async expectOnChildrenList() {
    await this.page.waitForURL('/children');
  }

  async expectChildVisible(name: string) {
    await expect(this.page.getByText(name).first()).toBeVisible();
  }
}
