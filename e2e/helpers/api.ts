// Allow self-signed certificates for local dev
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const API_URL = process.env.API_URL || 'http://localhost:7200';

export async function apiPost(path: string, body: any, token?: string) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });
  return res.json();
}

export async function apiGet(path: string, token?: string) {
  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, { headers });
  return res.json();
}

export async function apiPut(path: string, body: any, token?: string) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, {
    method: 'PUT',
    headers,
    body: JSON.stringify(body),
  });
  return res.json();
}

export async function loginAdmin(): Promise<string> {
  const email = process.env.ADMIN_EMAIL || 'admin@example.com';
  const password = process.env.ADMIN_PASSWORD || 'adminpassword';

  const data = await apiPost('/api/auth/login', { email, password });
  if (!data.data?.token) throw new Error('Admin login failed: ' + JSON.stringify(data));
  return data.data.token;
}

export async function generateBetaCode(adminToken: string): Promise<string> {
  const data = await apiPost('/api/admin/beta-codes', { count: 1 }, adminToken);
  if (!data.data || data.data.length === 0) throw new Error('Beta code generation failed');
  return data.data[0];
}

export async function registerUser(email: string, password: string, firstName: string, lastName: string, inviteCode: string) {
  return apiPost('/api/auth/register', { email, password, firstName, lastName, inviteCode });
}

export async function loginUser(email: string, password: string): Promise<string> {
  const data = await apiPost('/api/auth/login', { email, password });
  if (!data.data?.token) throw new Error('Login failed');
  return data.data.token;
}

export async function getCurrentUser(token: string): Promise<{ id: number }> {
  const data = await apiGet('/api/auth/me', token);
  return data.data;
}
