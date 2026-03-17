import { test as base } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import type { TestUser } from './test-data';

// Read the shared test user created in global setup
export function getSharedTestUser(): TestUser {
  const dataPath = path.join(__dirname, '..', '.test-data.json');
  return JSON.parse(fs.readFileSync(dataPath, 'utf-8'));
}

// Re-export — auth state is loaded via storageState in playwright.config.ts
export const test = base;
export { expect } from '@playwright/test';
