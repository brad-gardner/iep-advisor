import * as fs from 'fs';
import * as path from 'path';
import { apiPut } from './helpers/api';

async function globalTeardown() {
  const dataPath = path.join(__dirname, '.test-data.json');
  if (!fs.existsSync(dataPath)) return;

  const data = JSON.parse(fs.readFileSync(dataPath, 'utf-8'));

  if (data.adminToken && data.userId) {
    console.log(`Cleaning up: deactivating test user ${data.email} (ID: ${data.userId})`);
    try {
      await apiPut(`/api/users/${data.userId}`, { isActive: false }, data.adminToken);
      console.log('Test user deactivated');
    } catch (e) {
      console.warn('Failed to deactivate test user:', e);
    }
  }

  // Clean up temp files
  try {
    fs.unlinkSync(dataPath);
  } catch {}

  const authPath = path.join(__dirname, '.auth-state.json');
  try {
    if (fs.existsSync(authPath)) fs.unlinkSync(authPath);
  } catch {}
}

export default globalTeardown;
