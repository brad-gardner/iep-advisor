import { test as base } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import type { TestUser } from './test-data';

// Read the shared test user from global setup
export function getSharedTestUser(): TestUser {
  const dataPath = path.join(__dirname, '..', '.test-data.json');
  return JSON.parse(fs.readFileSync(dataPath, 'utf-8'));
}

// Re-export test and expect — auth state is loaded globally via storageState
// in playwright.config.ts, so every test already has a logged-in session.
// No per-test login needed.
export const test = base;
export { expect } from '@playwright/test';
