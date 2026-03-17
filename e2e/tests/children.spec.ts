import { test, expect } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';

test.describe('Child Management', () => {
  test('create child with all fields', async ({ page }) => {
    const children = new ChildrenPage(page);

    await children.gotoNew();
    await children.fillChildForm({
      firstName: 'Emma',
      lastName: 'TestChild',
      dateOfBirth: '2015-06-15',
      gradeLevel: '3rd',
      disabilityCategory: 'Autism',
      schoolDistrict: 'Test District',
    });
    await children.submitCreateForm();

    await children.expectOnChildrenList();
    await children.expectChildVisible('Emma');
  });

  test('edit child profile', async ({ page }) => {
    const children = new ChildrenPage(page);

    await children.goto();
    await children.clickChildByName('Emma');
    await children.clickEdit();
    await children.updateGradeLevel('4th');
    await children.submitEditForm();

    await children.expectChildVisible('4th');
  });
});
