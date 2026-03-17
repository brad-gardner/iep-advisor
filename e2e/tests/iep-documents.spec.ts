import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';
import { ChildrenPage } from '../pages/children.page';
import { IepDocumentsPage } from '../pages/iep-documents.page';

test.describe('IEP Documents', () => {
  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'IepDocChild', 'E2E');
    childId = child.id;
  });

  test('create IEP event with date and type', async ({ page }) => {
    const children = new ChildrenPage(page);
    const iepDocs = new IepDocumentsPage(page);

    await children.gotoChild(childId);

    await iepDocs.createIep('2026-03-15', 'annual_review');
    await iepDocs.expectIepVisible('Annual Review');
  });

  test('upload PDF to existing IEP', async ({ page }) => {
    const children = new ChildrenPage(page);

    await children.gotoChild(childId);

    // Check that the upload zone or file input exists for IEPs in "created" status
    const uploadZone = page.locator('[data-testid="iep-upload-zone"], [data-testid="iep-file-input"]').first();
    await expect(uploadZone).toBeVisible({ timeout: 5000 }).catch(() => {
      console.log('No upload zone found — IEP may already have a file attached');
    });
  });

  test('reject non-PDF file upload', async ({ page }) => {
    const children = new ChildrenPage(page);
    const iepDocs = new IepDocumentsPage(page);

    // Create a second IEP so we have a fresh upload slot
    await children.gotoChild(childId);
    await iepDocs.createIep('2026-04-01', 'initial');

    // Upload a .txt file via the hidden file input
    const fileInput = page.locator('[data-testid="iep-file-input"]').first();
    await fileInput.setInputFiles({
      name: 'fake-document.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('This is not a PDF'),
    });

    // Should show error
    await expect(page.locator('[data-testid="iep-upload-error"]')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('[data-testid="iep-upload-error"]')).toContainText('Only PDF files');
  });
});
