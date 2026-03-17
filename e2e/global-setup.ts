import { chromium } from '@playwright/test';
import { getAdminToken, createTestUser } from './helpers/test-data';
import * as fs from 'fs';
import * as path from 'path';

async function globalSetup() {
  console.log('Global setup: verifying admin access...');

  try {
    await getAdminToken();
    console.log('Admin login successful');
  } catch (e) {
    console.error('Admin login failed. Make sure an admin account exists and .env is configured.');
    throw e;
  }

  // Create a shared test user
  console.log('Creating shared test user...');
  const user = await createTestUser('shared');

  const testDataPath = path.join(__dirname, '.test-data.json');
  fs.writeFileSync(testDataPath, JSON.stringify(user, null, 2));
  console.log(`Test user created: ${user.email}`);

  // Login once via browser and save auth state (localStorage with JWT)
  console.log('Logging in via browser to save auth state...');
  const baseURL = process.env.BASE_URL || 'http://localhost:5200';
  const browser = await chromium.launch();
  const context = await browser.newContext();
  const page = await context.newPage();

  await page.goto(`${baseURL}/login`);
  await page.fill('input[type="email"]', user.email);
  await page.fill('input[type="password"]', user.password);
  await page.click('button[type="submit"]');

  // Wait for redirect — means login succeeded and JWT is in localStorage
  await page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });

  // Save the storage state (includes localStorage with JWT)
  const storagePath = path.join(__dirname, '.auth-state.json');
  await context.storageState({ path: storagePath });
  console.log('Auth state saved');

  await browser.close();
}

export default globalSetup;
