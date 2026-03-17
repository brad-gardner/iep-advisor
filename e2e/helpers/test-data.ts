import { loginAdmin, generateBetaCode, registerUser, loginUser } from './api';

export interface TestUser {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  inviteCode: string;
  token: string;
}

let _adminToken: string | null = null;

export async function getAdminToken(): Promise<string> {
  if (!_adminToken) _adminToken = await loginAdmin();
  return _adminToken;
}

export async function createTestUser(suffix?: string): Promise<TestUser> {
  const ts = Date.now();
  const id = suffix || ts.toString();
  const email = `test-${id}@e2e.test`;
  const password = 'TestPass123!';
  const firstName = 'Test';
  const lastName = `User${id}`;

  const adminToken = await getAdminToken();
  const inviteCode = await generateBetaCode(adminToken);

  await registerUser(email, password, firstName, lastName, inviteCode);
  const token = await loginUser(email, password);

  return { email, password, firstName, lastName, inviteCode, token };
}
