import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { generateBetaCode } from '../helpers/api';
import { getSharedTestUser } from '../helpers/fixtures';
import { LoginPage } from '../pages/login.page';
import { RegisterPage } from '../pages/register.page';
import { Sidebar } from '../pages/sidebar.page';

// Auth tests run without saved auth state and need HTTPS errors ignored
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

test.describe('Authentication', () => {
  let betaCode: string;

  test.beforeAll(async () => {
    const adminToken = await getAdminToken();
    betaCode = await generateBetaCode(adminToken);
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

    // Should stay on register page (not redirect to login)
    await registerPage.expectStayedOnRegister();
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

    // Should stay on login page
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
});
