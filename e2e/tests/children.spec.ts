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
});
