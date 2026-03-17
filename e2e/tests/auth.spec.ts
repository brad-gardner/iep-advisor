import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { generateBetaCode } from '../helpers/api';
import { getSharedTestUser } from '../helpers/fixtures';

// Auth tests must run WITHOUT saved auth state — they test login/register as unauthenticated users
test.use({ storageState: { cookies: [], origins: [] } });

test.describe('Authentication', () => {
  test('register with valid beta code', async ({ page }) => {
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const email = `register-${Date.now()}@e2e.test`;

    await page.goto('/register');
    await page.getByLabel('Invite Code').fill(code);
    await page.getByLabel('First Name').fill('Test');
    await page.getByLabel('Last Name').fill('Register');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill('TestPass123!');
    await page.getByLabel('Confirm Password').fill('TestPass123!');
    await page.getByRole('button', { name: 'Create Account' }).click();

    // Should redirect to login with success message
    await page.waitForURL('/login');
    await expect(page.locator('text=Registration successful')).toBeVisible();
  });

  test('register with invalid code shows error', async ({ page }) => {
    await page.goto('/register');
    await page.getByLabel('Invite Code').fill('INVALIDX');
    await page.getByLabel('First Name').fill('Test');
    await page.getByLabel('Last Name').fill('Bad');
    await page.getByLabel('Email').fill(`bad-${Date.now()}@e2e.test`);
    await page.getByLabel('Password', { exact: true }).fill('TestPass123!');
    await page.getByLabel('Confirm Password').fill('TestPass123!');
    await page.getByRole('button', { name: 'Create Account' }).click();

    // Error should appear — check for any error notice
    await expect(page.getByText(/invalid|expired|failed/i).first()).toBeVisible({ timeout: 10000 });
  });

  test('login with valid credentials', async ({ page }) => {
    const user = getSharedTestUser();

    await page.goto('/login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    await page.waitForURL(/\/(dashboard|onboarding)/);
  });

  test('login with wrong password shows error', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill('nobody@e2e.test');
    await page.getByLabel('Password').fill('wrongpassword');
    await page.getByRole('button', { name: 'Sign In' }).click();

    await expect(page.getByText(/invalid|failed/i).first()).toBeVisible({ timeout: 10000 });
  });

  test('logout clears session', async ({ page }) => {
    const user = getSharedTestUser();

    await page.goto('/login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|onboarding)/);

    // Click sign out in sidebar
    await page.getByRole('button', { name: /sign out/i }).click();
    await page.waitForURL('/login');
  });
});
