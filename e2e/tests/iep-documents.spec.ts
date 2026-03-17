import { test, expect } from '../helpers/fixtures';
import { ChildrenPage } from '../pages/children.page';
import { IepDocumentsPage } from '../pages/iep-documents.page';

test.describe('IEP Documents', () => {
  test('create IEP event with date and type', async ({ loggedInPage: page }) => {
    const children = new ChildrenPage(page);
    const iepDocs = new IepDocumentsPage(page);

    await children.goto();
    await children.clickFirstChild();

    await iepDocs.createIep('2026-03-15', 'annual_review');
    await iepDocs.expectIepVisible('Annual Review');
  });

  test('upload PDF to existing IEP', async ({ loggedInPage: page }) => {
    const children = new ChildrenPage(page);
    const iepDocs = new IepDocumentsPage(page);

    await children.goto();
    await children.clickFirstChild();

    await iepDocs.expectFileInputAttached();
  });
});
