import { test as base, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import type { TestUser } from './test-data';

// Read the shared test user from global setup
function getSharedTestUser(): TestUser {
  const dataPath = path.join(__dirname, '..', '.test-data.json');
  return JSON.parse(fs.readFileSync(dataPath, 'utf-8'));
}

// Extended test fixture that provides a logged-in page
export const test = base.extend<{ loggedInPage: Page }>({
  loggedInPage: async ({ page }, use) => {
    const user = getSharedTestUser();

    await page.goto('/login');
    await page.fill('input[type="email"]', user.email);
    await page.fill('input[type="password"]', user.password);
    await page.click('button[type="submit"]');

    // Wait for redirect to dashboard or onboarding
    await page.waitForURL(/\/(dashboard|onboarding)/);

    await use(page);
  },
});

export { expect } from '@playwright/test';
export { getSharedTestUser };
