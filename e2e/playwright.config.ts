import { defineConfig, devices } from '@playwright/test';
import dotenv from 'dotenv';

dotenv.config();

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1,
  reporter: [['html', { open: 'never' }], ['list']],
  use: {
    baseURL: process.env.BASE_URL || 'https://localhost:5200',
    screenshot: 'only-on-failure',
    trace: 'on-first-retry',
    headless: true,
    ignoreHTTPSErrors: true,  // Self-signed certs on local dev
    storageState: '.auth-state.json',  // Reuse auth from global setup
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  globalSetup: './global-setup.ts',
  globalTeardown: './global-teardown.ts',
});
