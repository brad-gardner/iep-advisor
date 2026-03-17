import { test, expect } from '../helpers/fixtures';

test.describe('Smoke Tests - Every page loads', () => {
  test('dashboard loads', async ({ loggedInPage: page }) => {
    await page.goto('/dashboard');
    await expect(page.locator('h1')).toBeVisible();
  });

  test('children page loads', async ({ loggedInPage: page }) => {
    await page.goto('/children');
    await expect(page.locator('h1')).toBeVisible();
  });

  test('profile page loads', async ({ loggedInPage: page }) => {
    await page.goto('/profile');
    await expect(page.locator('h1')).toBeVisible();
  });

  test('knowledge base loads', async ({ loggedInPage: page }) => {
    await page.goto('/knowledge-base');
    await expect(page.locator('h1')).toBeVisible();
  });

  test('IEP 101 loads', async ({ loggedInPage: page }) => {
    await page.goto('/iep-101');
    await expect(page.locator('h1')).toBeVisible();
  });

  test('subscription page loads', async ({ loggedInPage: page }) => {
    await page.goto('/subscription');
    await expect(page.locator('h1')).toBeVisible();
  });
});
