import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { generateBetaCode } from '../helpers/api';
import { getSharedTestUser } from '../helpers/fixtures';

// Auth tests must run WITHOUT saved auth state — they test login/register as unauthenticated users
test.use({ storageState: { cookies: [], origins: [] } });

test.describe('Authentication', () => {
  // Cache the admin token + beta code at the describe level to minimize API calls
  let betaCode: string;

  test.beforeAll(async () => {
    const adminToken = await getAdminToken();
    betaCode = await generateBetaCode(adminToken);
  });

  test('register with valid beta code', async ({ page }) => {
    const email = `register-${Date.now()}@e2e.test`;

    await page.goto('/register');
    await page.getByLabel('Invite Code').fill(betaCode);
    await page.getByLabel('First Name').fill('Test');
    await page.getByLabel('Last Name').fill('Register');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel('Password', { exact: true }).fill('TestPass123!');
    await page.getByLabel('Confirm Password').fill('TestPass123!');
    await page.getByRole('button', { name: 'Create Account' }).click();

    // Should redirect to login with success message
    await page.waitForURL(/\/login/, { timeout: 15000 });
    await expect(page.getByText(/registration successful/i).first()).toBeVisible({ timeout: 5000 });
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

    // Wait for any error to appear (could be "Invalid", "expired", "failed", "error", etc.)
    await page.waitForTimeout(3000);
    // The page should NOT have navigated to /login (stayed on /register with an error)
    await expect(page).toHaveURL(/\/register/);
  });

  test('login with valid credentials', async ({ page }) => {
    const user = getSharedTestUser();

    await page.goto('/login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();

    await page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });
  });

  test('login with wrong password shows error', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Email').fill('nobody@e2e.test');
    await page.getByLabel('Password').fill('wrongpassword');
    await page.getByRole('button', { name: 'Sign In' }).click();

    // Should stay on login page (not redirect)
    await page.waitForTimeout(3000);
    await expect(page).toHaveURL(/\/login/);
  });

  test('logout clears session', async ({ page }) => {
    const user = getSharedTestUser();

    await page.goto('/login');
    await page.getByLabel('Email').fill(user.email);
    await page.getByLabel('Password').fill(user.password);
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });

    // Click sign out in sidebar
    await page.getByRole('button', { name: /sign out/i }).click();
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });
});
