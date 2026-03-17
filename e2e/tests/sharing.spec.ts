import { test, expect, getSharedTestUser } from '../helpers/fixtures';
import { createChildViaApi } from '../helpers/api';
import { ChildrenPage } from '../pages/children.page';

test.describe('Sharing & Access', () => {
  let childId: number;

  test.beforeAll(async () => {
    const user = getSharedTestUser();
    const child = await createChildViaApi(user.token, 'ShareChild', 'E2E');
    childId = child.id;
  });

  test('sharing section visible on child detail for owner', async ({ page }) => {
    const children = new ChildrenPage(page);
    await children.gotoChild(childId);

    await expect(page.locator('[data-testid="sharing-section"]')).toBeVisible();
    await expect(page.locator('[data-testid="share-invite-button"]')).toBeVisible();
  });

  test('owner can see access list with self as Owner', async ({ page }) => {
    const children = new ChildrenPage(page);
    await children.gotoChild(childId);

    // Access list should show the owner
    const sharingSection = page.locator('[data-testid="sharing-section"]');
    await expect(sharingSection.locator('text=Owner')).toBeVisible();
  });

  test('invite dialog opens and accepts email input', async ({ page }) => {
    const children = new ChildrenPage(page);
    await children.gotoChild(childId);

    // Open share dialog
    await page.locator('[data-testid="share-invite-button"]').click();
    await expect(page.locator('[data-testid="share-email"]')).toBeVisible();
    await expect(page.locator('[data-testid="share-role"]')).toBeVisible();
    await expect(page.locator('[data-testid="share-submit"]')).toBeVisible();

    // Verify role dropdown has viewer and collaborator options
    const roleSelect = page.locator('[data-testid="share-role"]');
    await expect(roleSelect.locator('option[value="viewer"]')).toBeAttached();
    await expect(roleSelect.locator('option[value="collaborator"]')).toBeAttached();
  });

  test('send invite to another email', async ({ page }) => {
    const children = new ChildrenPage(page);
    await children.gotoChild(childId);

    // Open share dialog
    await page.locator('[data-testid="share-invite-button"]').click();

    // Fill in invite details
    const inviteEmail = `share-viewer-${Date.now()}@e2e.test`;
    await page.locator('[data-testid="share-email"]').fill(inviteEmail);
    await page.locator('[data-testid="share-role"]').selectOption('viewer');
    await page.locator('[data-testid="share-submit"]').click();

    // Verify invite appeared in access list (dialog closes after success)
    await expect(page.locator(`text=${inviteEmail}`)).toBeVisible({ timeout: 5000 });
    await expect(page.locator('[data-testid="sharing-section"]').getByText('Pending', { exact: true })).toBeVisible();
  });

  test('access list shows pending invite after sharing', async ({ page }) => {
    const children = new ChildrenPage(page);
    await children.gotoChild(childId);

    // Should see "Pending" badge for the invite we sent in earlier test
    const sharingSection = page.locator('[data-testid="sharing-section"]');
    await expect(sharingSection.getByText('Pending', { exact: true })).toBeVisible({ timeout: 5000 });
  });

  test('owner can revoke access', async ({ page }) => {
    const children = new ChildrenPage(page);
    await children.gotoChild(childId);

    // There should be revoke buttons from invites sent in previous tests
    await expect(page.locator('[data-testid="revoke-access"]').first()).toBeVisible({ timeout: 5000 });
    const countBefore = await page.locator('[data-testid="revoke-access"]').count();

    // Revoke the first non-owner access
    page.on('dialog', (dialog) => dialog.accept());
    await page.locator('[data-testid="revoke-access"]').first().click();
    await page.waitForTimeout(1500);

    // Should have one fewer revoke button
    const countAfter = await page.locator('[data-testid="revoke-access"]').count();
    expect(countAfter).toBe(countBefore - 1);
  });
});
