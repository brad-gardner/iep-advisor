import { test, expect } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';

test.describe('Child Management', () => {
  const childName = `Child${Date.now()}`;

  test('create child with all fields', async ({ page }) => {
    const children = new ChildrenPage(page);

    await children.gotoNew();
    await children.fillChildForm({
      firstName: childName,
      lastName: 'TestChild',
      dateOfBirth: '2015-06-15',
      gradeLevel: '3rd',
      disabilityCategory: 'Autism',
      schoolDistrict: 'Test District',
    });
    await children.submitCreateForm();

    await children.expectOnChildrenList();
    await children.expectChildVisible(childName);
  });

  test('edit child profile', async ({ page }) => {
    const children = new ChildrenPage(page);

    await children.goto();
    await children.clickChildByName(childName);
    await children.clickEdit();
    await children.updateGradeLevel('4th');
    await children.submitEditForm();

    await children.expectChildVisible('4th');
  });

  test('create child with first name only', async ({ page }) => {
    const children = new ChildrenPage(page);
    const minimalName = `Minimal${Date.now()}`;

    await children.gotoNew();
    await children.fillChildForm({ firstName: minimalName });
    await children.submitCreateForm();

    await children.expectOnChildrenList();
    await children.expectChildVisible(minimalName);
  });

  test('remove child with confirmation', async ({ page }) => {
    const children = new ChildrenPage(page);
    const removeName = `Remove${Date.now()}`;

    // Create a child to remove
    await children.gotoNew();
    await children.fillChildForm({ firstName: removeName });
    await children.submitCreateForm();
    await children.expectOnChildrenList();

    // Navigate to the child and remove
    await children.clickChildByName(removeName);
    await children.clickRemove();

    // Should redirect to children list
    await children.expectOnChildrenList();

    // Child card should no longer appear in the list
    await expect(page.locator(`[data-testid="child-card"]:has-text("${removeName}")`)).not.toBeVisible();
  });
});
