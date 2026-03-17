import { test, expect } from '@playwright/test';
import { getAdminToken, createTestUser } from '../helpers/test-data';
import { generateBetaCode } from '../helpers/api';

test.describe('Authentication', () => {
  test('register with valid beta code', async ({ page }) => {
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const email = `register-${Date.now()}@e2e.test`;

    await page.goto('/register');
    await page.fill('input[name="inviteCode"]', code);
    await page.fill('input[name="firstName"]', 'Test');
    await page.fill('input[name="lastName"]', 'Register');
    await page.fill('input[name="email"]', email);
    await page.fill('input[name="password"]', 'TestPass123!');
    await page.fill('input[name="confirmPassword"]', 'TestPass123!');
    await page.click('button[type="submit"]');

    // Should redirect to login with success message
    await page.waitForURL('/login');
    await expect(page.locator('text=Registration successful')).toBeVisible();
  });

  test('register with invalid code shows error', async ({ page }) => {
    await page.goto('/register');
    await page.fill('input[name="inviteCode"]', 'INVALIDX');
    await page.fill('input[name="firstName"]', 'Test');
    await page.fill('input[name="lastName"]', 'Bad');
    await page.fill('input[name="email"]', `bad-${Date.now()}@e2e.test`);
    await page.fill('input[name="password"]', 'TestPass123!');
    await page.fill('input[name="confirmPassword"]', 'TestPass123!');
    await page.click('button[type="submit"]');

    await expect(page.locator('text=Invalid')).toBeVisible({ timeout: 5000 });
  });

  test('login with valid credentials', async ({ page }) => {
    const user = await createTestUser('login');

    await page.goto('/login');
    await page.fill('input[type="email"]', user.email);
    await page.fill('input[type="password"]', user.password);
    await page.click('button[type="submit"]');

    await page.waitForURL(/\/(dashboard|onboarding)/);
  });

  test('login with wrong password shows error', async ({ page }) => {
    await page.goto('/login');
    await page.fill('input[type="email"]', 'nobody@e2e.test');
    await page.fill('input[type="password"]', 'wrongpassword');
    await page.click('button[type="submit"]');

    await expect(page.locator('text=Invalid email or password')).toBeVisible({ timeout: 5000 });
  });

  test('logout clears session', async ({ page }) => {
    const user = await createTestUser('logout');

    await page.goto('/login');
    await page.fill('input[type="email"]', user.email);
    await page.fill('input[type="password"]', user.password);
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/(dashboard|onboarding)/);

    // Click sign out in sidebar
    await page.click('text=Sign Out');
    await page.waitForURL('/login');
  });
});
