import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { generateBetaCode, registerUser } from '../helpers/api';
import { getSharedTestUser } from '../helpers/fixtures';
import { LoginPage } from '../pages/login.page';
import { RegisterPage } from '../pages/register.page';
import { Sidebar } from '../pages/sidebar.page';

// Auth tests run without saved auth state and need HTTPS errors ignored
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

test.describe('Authentication', () => {
  let betaCode: string;
  let usedBetaCode: string;
  let registeredEmail: string;

  test.beforeAll(async () => {
    const adminToken = await getAdminToken();
    betaCode = await generateBetaCode(adminToken);

    // Pre-register a user so we can test duplicate email and reused code
    usedBetaCode = await generateBetaCode(adminToken);
    registeredEmail = `preregistered-${Date.now()}@e2e.test`;
    await registerUser(registeredEmail, 'TestPass123!', 'Pre', 'Registered', usedBetaCode);
  });

  test('register with valid beta code', async ({ page }) => {
    const email = `register-${Date.now()}@e2e.test`;
    const registerPage = new RegisterPage(page);
    const loginPage = new LoginPage(page);

    await registerPage.goto();
    await registerPage.fillForm({
      inviteCode: betaCode,
      firstName: 'Test',
      lastName: 'Register',
      email,
      password: 'TestPass123!',
    });
    await registerPage.submit();

    await registerPage.expectRedirectToLogin();
    await loginPage.expectSuccessMessage();
  });

  test('register with invalid code shows error', async ({ page }) => {
    const registerPage = new RegisterPage(page);

    await registerPage.goto();
    await registerPage.fillForm({
      inviteCode: 'INVALIDX',
      firstName: 'Test',
      lastName: 'Bad',
      email: `bad-${Date.now()}@e2e.test`,
      password: 'TestPass123!',
    });
    await registerPage.submit();

    await registerPage.expectStayedOnRegister();
    await expect(page.locator('[data-testid="register-error"]')).toBeVisible();
  });

  test('register with already-used invite code shows error', async ({ page }) => {
    const registerPage = new RegisterPage(page);

    await registerPage.goto();
    await registerPage.fillForm({
      inviteCode: usedBetaCode,
      firstName: 'Reuse',
      lastName: 'Code',
      email: `reuse-${Date.now()}@e2e.test`,
      password: 'TestPass123!',
    });
    await registerPage.submit();

    await registerPage.expectStayedOnRegister();
    await expect(page.locator('[data-testid="register-error"]')).toBeVisible();
  });

  test('register with duplicate email shows error', async ({ page }) => {
    const adminToken = await getAdminToken();
    const freshCode = await generateBetaCode(adminToken);
    const registerPage = new RegisterPage(page);

    await registerPage.goto();
    await registerPage.fillForm({
      inviteCode: freshCode,
      firstName: 'Dupe',
      lastName: 'Email',
      email: registeredEmail,
      password: 'TestPass123!',
    });
    await registerPage.submit();

    await registerPage.expectStayedOnRegister();
    await expect(page.locator('[data-testid="register-error"]')).toBeVisible();
  });

  test('login with valid credentials', async ({ page }) => {
    const user = getSharedTestUser();
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.login(user.email, user.password);
    await loginPage.expectRedirectToDashboard();
  });

  test('login with wrong password shows error', async ({ page }) => {
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.login('nobody@e2e.test', 'wrongpassword');

    await loginPage.expectStayedOnLogin();
  });

  test('login with non-existent email shows same error (no enumeration)', async ({ page }) => {
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.login(`nonexistent-${Date.now()}@e2e.test`, 'SomePass123!');

    await loginPage.expectStayedOnLogin();
  });

  test('logout clears session', async ({ page }) => {
    const user = getSharedTestUser();
    const loginPage = new LoginPage(page);
    const sidebar = new Sidebar(page);

    await loginPage.goto();
    await loginPage.login(user.email, user.password);
    await loginPage.expectRedirectToDashboard();

    await sidebar.signOut();
    await page.waitForURL(/\/login/, { timeout: 10000 });
  });

  test('forgot password link navigates to reset page', async ({ page }) => {
    await page.goto('/login');
    await page.locator('[data-testid="forgot-password-link"]').click();
    await page.waitForURL(/\/forgot-password/);
    await expect(page.locator('[data-testid="forgot-submit"]')).toBeVisible();
  });
});
