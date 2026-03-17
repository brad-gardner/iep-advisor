import { chromium } from '@playwright/test';
import { getAdminToken, createTestUser } from './helpers/test-data';
import * as fs from 'fs';
import * as path from 'path';

async function globalSetup() {
  console.log('Global setup: verifying admin access...');

  let adminToken: string;
  try {
    adminToken = await getAdminToken();
    console.log('Admin login successful');
  } catch (e) {
    console.error('Admin login failed. Make sure an admin account exists and .env is configured.');
    throw e;
  }

  // Create a shared test user
  console.log('Creating shared test user...');
  const user = await createTestUser();

  // Store test user info and admin token for tests and teardown
  const testDataPath = path.join(__dirname, '.test-data.json');
  fs.writeFileSync(testDataPath, JSON.stringify({ ...user, adminToken }, null, 2));
  console.log(`Test user created: ${user.email} (ID: ${user.userId})`);

  // Login via browser and save auth state (localStorage with JWT)
  console.log('Logging in via browser to save auth state...');
  const baseURL = process.env.BASE_URL || 'https://localhost:5200';
  const browser = await chromium.launch();
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  await page.goto(`${baseURL}/login`);
  await page.getByLabel('Email').fill(user.email);
  await page.getByLabel('Password').fill(user.password);
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/(dashboard|onboarding)/, { timeout: 15000 });

  // Save storage state (includes localStorage with JWT)
  const storagePath = path.join(__dirname, '.auth-state.json');
  await context.storageState({ path: storagePath });
  console.log('Auth state saved');

  await browser.close();
}

export default globalSetup;
