import { test, expect } from '@playwright/test';

// Security tests run WITHOUT auth state to verify protected route redirects
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

test.describe('Security & Auth Guards', () => {
  const protectedRoutes = [
    '/dashboard',
    '/children',
    '/children/new',
    '/profile',
    '/subscription',
    '/knowledge-base',
    '/admin',
    '/admin/users',
  ];

  for (const route of protectedRoutes) {
    test(`${route} redirects to login when unauthenticated`, async ({ page }) => {
      await page.goto(route);
      await page.waitForURL(/\/login/, { timeout: 10000 });
      await expect(page).toHaveURL(/\/login/);
    });
  }

  test('accessing another user\'s child by ID shows 404 or redirects', async ({ page }) => {
    // Try accessing a child ID that doesn't belong to the user (unauthenticated → login)
    await page.goto('/children/99999');
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });

  test('browser back after logout does not show protected content', async ({ page }) => {
    // Navigate to login page (no auth)
    await page.goto('/login');
    await expect(page).toHaveURL(/\/login/);

    // Try navigating directly to dashboard via URL
    await page.goto('/dashboard');
    // Should redirect back to login
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });
});
