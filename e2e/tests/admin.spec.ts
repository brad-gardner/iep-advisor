import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/login.page';

// Admin tests need their own auth — clear shared state
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

test.describe('Admin Features', () => {
  const adminEmail = process.env.ADMIN_EMAIL || 'admin@example.com';
  const adminPassword = process.env.ADMIN_PASSWORD || 'adminpassword';

  test.beforeEach(async ({ page }) => {
    const login = new LoginPage(page);
    await login.goto();
    await login.login(adminEmail, adminPassword);
    await login.expectRedirectToDashboard();
  });

  test('admin sidebar shows admin links', async ({ page }) => {
    // Admin links are at bottom of sidebar — verify they exist in DOM (may be below fold)
    await expect(page.locator('[data-testid="nav-admin-dashboard"]').first()).toBeAttached();
    await expect(page.locator('[data-testid="nav-admin-users"]').first()).toBeAttached();
  });

  test('admin dashboard loads with stats', async ({ page }) => {
    await page.goto('/admin');

    await expect(page.locator('[data-testid="admin-dashboard"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-stat-total-users"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-stat-active-children"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-stat-iep-documents"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-stat-ai-analyses"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-recent-users"]')).toBeVisible();
  });

  test('admin users page lists users with search', async ({ page }) => {
    await page.goto('/admin/users');

    // Search input should be visible
    await expect(page.locator('[data-testid="admin-user-search"]')).toBeVisible();

    // Should have at least one user row
    await expect(page.locator('main').getByText(adminEmail)).toBeVisible();

    // Search filters results
    await page.locator('[data-testid="admin-user-search"]').fill('nonexistent-user-xyz');
    await page.waitForTimeout(500);
    // Admin email should no longer be visible after filtering
    await expect(page.locator('main').getByText(adminEmail)).not.toBeVisible({ timeout: 3000 });
  });

  test('admin can invite beta user', async ({ page }) => {
    await page.goto('/admin/users');

    await page.locator('[data-testid="admin-invite-button"]').click();
    await expect(page.locator('[data-testid="admin-invite-email"]')).toBeVisible();

    await page.locator('[data-testid="admin-invite-email"]').fill(`invite-test-${Date.now()}@e2e.test`);
    await page.locator('[data-testid="admin-send-invite"]').click();

    // Should show success message
    await expect(page.locator('text=Invite sent')).toBeVisible({ timeout: 5000 });
  });

  test('admin user detail page loads', async ({ page }) => {
    await page.goto('/admin/users');
    // Click on a user row — target buttons that contain user email text
    await page.locator('button:has-text("@")').first().click();
    await page.waitForURL(/\/admin\/users\/\d+/);

    await expect(page.locator('[data-testid="admin-user-role"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-user-active"]')).toBeVisible();
    await expect(page.locator('[data-testid="admin-user-save"]')).toBeVisible();
  });
});

// Separate describe for non-admin access check
test.describe('Admin Access Control', () => {
  test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

  test('non-admin redirected from admin pages', async ({ page }) => {
    // Register a fresh non-admin user
    const { getAdminToken } = await import('../helpers/test-data');
    const { generateBetaCode, registerUser, loginUser } = await import('../helpers/api');

    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const email = `nonadmin-${Date.now()}@e2e.test`;
    await registerUser(email, 'TestPass123!', 'NonAdmin', 'User', code);

    // Login as non-admin
    const login = new LoginPage(page);
    await login.goto();
    await login.login(email, 'TestPass123!');
    await login.expectRedirectToDashboard();

    // Try to access admin — should redirect
    await page.goto('/admin');
    await page.waitForTimeout(2000);
    await expect(page).not.toHaveURL(/\/admin/);
  });
});
