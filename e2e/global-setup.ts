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

  // Create a shared test user for tests that need a pre-existing account
  console.log('Creating shared test user...');
  const user = await createTestUser('shared');

  // Store test user info and admin token for tests and teardown to read
  const testDataPath = path.join(__dirname, '.test-data.json');
  fs.writeFileSync(
    testDataPath,
    JSON.stringify({ ...user, adminToken }, null, 2),
  );
  console.log(`Test user created: ${user.email} (ID: ${user.userId})`);
}

export default globalSetup;
