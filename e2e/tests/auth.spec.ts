import { test, expect } from '@playwright/test';
import { getAdminToken, createTestUser } from '../helpers/test-data';
import { generateBetaCode } from '../helpers/api';
import { LoginPage } from '../pages/login.page';
import { RegisterPage } from '../pages/register.page';
import { Sidebar } from '../pages/sidebar.page';

test.describe('Authentication', () => {
  test('register with valid beta code', async ({ page }) => {
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const email = `register-${Date.now()}@e2e.test`;

    const registerPage = new RegisterPage(page);
    const loginPage = new LoginPage(page);

    await registerPage.goto();
    await registerPage.fillForm({
      inviteCode: code,
      firstName: 'Test',
      lastName: 'Register',
      email,
      password: 'TestPass123!',
    });
    await registerPage.submit();

    await loginPage.expectOnLoginPage();
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

    await registerPage.expectError('Invalid');
  });

  test('login with valid credentials', async ({ page }) => {
    const user = await createTestUser('login');
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.login(user.email, user.password);
    await loginPage.expectRedirectToDashboard();
  });

  test('login with wrong password shows error', async ({ page }) => {
    const loginPage = new LoginPage(page);

    await loginPage.goto();
    await loginPage.login('nobody@e2e.test', 'wrongpassword');
    await loginPage.expectError('Invalid email or password');
  });

  test('logout clears session', async ({ page }) => {
    const user = await createTestUser('logout');
    const loginPage = new LoginPage(page);
    const sidebar = new Sidebar(page);

    await loginPage.goto();
    await loginPage.login(user.email, user.password);
    await loginPage.expectRedirectToDashboard();

    await sidebar.signOut();
    await loginPage.expectOnLoginPage();
  });
});
