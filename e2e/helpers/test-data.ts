import { loginAdmin, generateBetaCode, registerUser, loginUser, getCurrentUser } from './api';

export interface TestUser {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  inviteCode: string;
  token: string;
  userId: number;
}

let _adminToken: string | null = null;

export async function getAdminToken(): Promise<string> {
  if (!_adminToken) _adminToken = await loginAdmin();
  return _adminToken;
}

export async function createTestUser(suffix?: string): Promise<TestUser> {
  // Always use a unique email to avoid collisions with previous test runs
  const ts = Date.now();
  const email = `test-${ts}@e2e.test`;
  const password = 'TestPass123!';
  const firstName = 'Test';
  const lastName = `User${ts}`;

  const adminToken = await getAdminToken();
  const inviteCode = await generateBetaCode(adminToken);

  const regResult = await registerUser(email, password, firstName, lastName, inviteCode);
  if (!regResult.success) {
    throw new Error(`Registration failed for ${email}: ${regResult.message}`);
  }

  const token = await loginUser(email, password);
  const user = await getCurrentUser(token);

  return { email, password, firstName, lastName, inviteCode, token, userId: user.id };
}
