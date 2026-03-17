import { test, expect } from '@playwright/test';
import { getAdminToken } from '../helpers/test-data';
import { generateBetaCode, registerUser } from '../helpers/api';
import { LoginPage } from '../pages/login.page';
import { OnboardingPage } from '../pages/onboarding.page';

// Fresh user needed — clear shared auth state so we can login as the new user
test.use({ storageState: { cookies: [], origins: [] }, ignoreHTTPSErrors: true });

test.describe('Onboarding', () => {
  const password = 'TestPass123!';
  let email: string;

  test.beforeAll(async () => {
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    email = `onboard-${Date.now()}@e2e.test`;
    const result = await registerUser(email, password, 'Onboard', 'Tester', code);
    if (!result.success && !result.data) {
      throw new Error('Registration failed: ' + JSON.stringify(result));
    }
  });

  test('full onboarding flow with state and child', async ({ page }) => {
    const login = new LoginPage(page);
    const onboarding = new OnboardingPage(page);

    // Login as fresh user — should redirect to dashboard with onboarding banner
    await login.goto();
    await login.login(email, password);
    await login.expectRedirectToDashboard();

    // Dashboard should show onboarding banner
    await expect(page.locator('[data-testid="onboarding-banner"]')).toBeVisible();

    // Click "Get Started"
    await page.locator('[data-testid="onboarding-get-started"]').click();
    await page.waitForURL(/\/onboarding/);

    // Step 1: Welcome
    await onboarding.expectWelcomeStep();
    await onboarding.clickStart();

    // Step 2: Set State
    await onboarding.expectStateStep();
    await onboarding.selectState('CA');
    await onboarding.clickContinueState();

    // Step 3: Add Child
    await onboarding.expectChildStep();
    await onboarding.fillChildName('OnboardChild');
    await onboarding.submitChild();

    // Step 4: Next Steps
    await onboarding.expectNextSteps();
    await onboarding.clickFinish();

    // Should be on dashboard with banner gone
    await page.waitForURL(/\/dashboard/);
    await expect(page.locator('[data-testid="onboarding-banner"]')).not.toBeVisible();
  });

  test('skip state and child steps', async ({ page }) => {
    // Register another fresh user for this test
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const skipEmail = `onboard-skip-${Date.now()}@e2e.test`;
    const result = await registerUser(skipEmail, password, 'Skip', 'Tester', code);
    if (!result.success && !result.data) {
      throw new Error('Registration failed: ' + JSON.stringify(result));
    }

    const login = new LoginPage(page);
    const onboarding = new OnboardingPage(page);

    await login.goto();
    await login.login(skipEmail, password);
    await login.expectRedirectToDashboard();

    await page.locator('[data-testid="onboarding-get-started"]').click();
    await page.waitForURL(/\/onboarding/);

    // Step 1: Welcome
    await onboarding.clickStart();

    // Step 2: Skip state
    await onboarding.expectStateStep();
    await onboarding.clickSkipState();

    // Step 3: Skip child
    await onboarding.expectChildStep();
    await onboarding.clickSkipChild();

    // Step 4: Finish
    await onboarding.expectNextSteps();
    await onboarding.clickFinish();

    await page.waitForURL(/\/dashboard/);
    await expect(page.locator('[data-testid="onboarding-banner"]')).not.toBeVisible();
  });

  test('progress dots update with each step', async ({ page }) => {
    // Register another fresh user
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const dotsEmail = `onboard-dots-${Date.now()}@e2e.test`;
    const result = await registerUser(dotsEmail, password, 'Dots', 'Tester', code);
    if (!result.success && !result.data) {
      throw new Error('Registration failed: ' + JSON.stringify(result));
    }

    const login = new LoginPage(page);
    const onboarding = new OnboardingPage(page);

    await login.goto();
    await login.login(dotsEmail, password);
    await login.expectRedirectToDashboard();

    await page.locator('[data-testid="onboarding-get-started"]').click();
    await page.waitForURL(/\/onboarding/);

    // Verify progress dots at each step
    await expect(page.locator('text=Step 1 of 4')).toBeVisible();
    await onboarding.clickStart();

    await expect(page.locator('text=Step 2 of 4')).toBeVisible();
    await onboarding.clickSkipState();

    await expect(page.locator('text=Step 3 of 4')).toBeVisible();
    await onboarding.clickSkipChild();

    await expect(page.locator('text=Step 4 of 4')).toBeVisible();
  });

  test('revisit onboarding after completion', async ({ page }) => {
    // Register another fresh user
    const adminToken = await getAdminToken();
    const code = await generateBetaCode(adminToken);
    const revisitEmail = `onboard-revisit-${Date.now()}@e2e.test`;
    const result = await registerUser(revisitEmail, password, 'Revisit', 'Tester', code);
    if (!result.success && !result.data) {
      throw new Error('Registration failed: ' + JSON.stringify(result));
    }

    const login = new LoginPage(page);
    const onboarding = new OnboardingPage(page);

    await login.goto();
    await login.login(revisitEmail, password);
    await login.expectRedirectToDashboard();

    // Complete onboarding quickly
    await page.locator('[data-testid="onboarding-get-started"]').click();
    await page.waitForURL(/\/onboarding/);
    await onboarding.clickStart();
    await onboarding.clickSkipState();
    await onboarding.clickSkipChild();
    await onboarding.clickFinish();
    await page.waitForURL(/\/dashboard/);

    // Revisit onboarding — should still load (not blocked)
    await onboarding.goto();
    await onboarding.expectWelcomeStep();
  });
});
