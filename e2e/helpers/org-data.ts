// Helpers for the school self-serve signup + org-management e2e specs.
//
// These drive the org-side API directly (register-district, schools, staff
// invites, students, staff-access) so the UI specs can set up state quickly and
// the API specs can assert authorization/scoping behavior. The API runs in
// Development with Email:ExposeLinksForTesting=true, so invite-creating
// responses include an `inviteUrl` we can mine for the raw token.

import { apiGet, apiPut } from './api';

// api.ts sets NODE_TLS_REJECT_UNAUTHORIZED=0 on import; mirror its API_URL.
const API_URL = process.env.API_URL || 'https://localhost:7200';

// Stable seeded OrgRole ids (api/IepAssistant.Services/Models/OrgRoleIds.cs).
export const ORG_ROLE = {
  DistrictAdmin: 1,
  SchoolAdmin: 2,
  Teacher: 3,
} as const;

export const STAFF_PASSWORD = 'TestPass123!';

// ---- low-level request helpers that surface the HTTP status ----------------

export interface ApiResult<T = any> {
  status: number;
  success: boolean;
  message: string | null;
  data: T;
  errors: any[];
}

async function request<T = any>(
  method: string,
  path: string,
  body?: any,
  token?: string
): Promise<ApiResult<T>> {
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (res.status === 429) {
    throw new Error(`Rate limited (429) on ${path}. Wait and retry.`);
  }

  const text = await res.text();
  let parsed: any = {};
  if (text) {
    try {
      parsed = JSON.parse(text);
    } catch {
      throw new Error(`Non-JSON response (${res.status}) from ${path}: ${text.substring(0, 200)}`);
    }
  }

  return {
    status: res.status,
    success: parsed.success ?? res.ok,
    message: parsed.message ?? null,
    data: parsed.data,
    errors: parsed.errors ?? [],
  };
}

// rawGet returns the HTTP status only — handy for "old JWT should now 401" checks.
export async function rawStatus(method: string, path: string, token?: string): Promise<number> {
  const res = await request(method, path, undefined, token);
  return res.status;
}

// ---- identities -------------------------------------------------------------

export interface DistrictAdminAccount {
  token: string;
  userId: number;
  email: string;
  password: string;
  districtName: string;
  staffProfileId: number;
}

let _seq = 0;
function uniqueEmail(prefix: string): string {
  // Date.now() can collide inside a tight loop; add a per-process counter.
  _seq += 1;
  return `${prefix}-${Date.now()}-${_seq}@e2e.test`;
}

export async function registerDistrictAdmin(
  opts: { districtName?: string; stateCode?: string } = {}
): Promise<DistrictAdminAccount> {
  const email = uniqueEmail('district-admin');
  const password = STAFF_PASSWORD;
  const districtName = opts.districtName ?? `E2E District ${Date.now()}-${_seq}`;

  const res = await request('POST', '/api/auth/register-district', {
    email,
    password,
    firstName: 'Dist',
    lastName: 'Admin',
    districtName,
    stateCode: opts.stateCode,
  });

  if (!res.success || !res.data?.token) {
    throw new Error(`register-district failed: ${JSON.stringify(res).substring(0, 300)}`);
  }

  const token: string = res.data.token;
  const userId: number = res.data.user.id;

  // The district admin's own staff profile id (needed for self-deactivation tests).
  const me = await request('GET', '/api/educator/me', undefined, token);
  const staffProfileId: number = me.data?.staffProfileId;

  return { token, userId, email, password, districtName, staffProfileId };
}

// ---- schools ----------------------------------------------------------------

export interface SchoolRef {
  id: number;
  name: string;
  stateCode?: string | null;
}

export async function createSchoolViaApi(
  token: string,
  name: string,
  stateCode?: string
): Promise<SchoolRef> {
  const res = await request('POST', '/api/district/schools', { name, stateCode }, token);
  if (!res.success || !res.data?.id) {
    throw new Error(`create school failed: ${JSON.stringify(res).substring(0, 300)}`);
  }
  return { id: res.data.id, name: res.data.name, stateCode: res.data.stateCode };
}

// ---- staff invites ----------------------------------------------------------

export interface StaffInviteResult {
  id: number;
  email: string;
  orgRoleId: number;
  schoolId: number | null;
  inviteUrl: string | null;
  inviteToken: string | null;
}

// Pull the (decoded) `token` query param out of an inviteUrl. The backend
// URL-encodes the base64 token, so the raw value contains '/', '+', '=' which
// MUST be decoded before re-sending to preview/accept.
export function extractInviteToken(inviteUrl: string): string {
  const q = new URL(inviteUrl).searchParams.get('token');
  if (!q) throw new Error(`No token in inviteUrl: ${inviteUrl}`);
  return q; // URL.searchParams already decodes the value.
}

export async function inviteStaffViaApi(
  token: string,
  opts: { email?: string; orgRoleId: number; schoolId?: number | null }
): Promise<StaffInviteResult & { raw: ApiResult }> {
  const email = opts.email ?? uniqueEmail('staff');
  const body: any = { email, orgRoleId: opts.orgRoleId };
  if (opts.schoolId != null) body.schoolId = opts.schoolId;

  const res = await request('POST', '/api/district/staff/invites', body, token);
  const inviteUrl: string | null = res.data?.inviteUrl ?? null;

  return {
    id: res.data?.id,
    email,
    orgRoleId: opts.orgRoleId,
    schoolId: res.data?.schoolId ?? null,
    inviteUrl,
    inviteToken: inviteUrl ? extractInviteToken(inviteUrl) : null,
    raw: res,
  };
}

export async function previewStaffInviteViaApi(inviteToken: string): Promise<ApiResult> {
  const path = `/api/staff-invites/preview?token=${encodeURIComponent(inviteToken)}`;
  return request('GET', path);
}

export interface AcceptedStaff {
  token: string;
  userId: number;
  email: string;
  password: string;
}

export async function acceptStaffInviteViaApi(
  inviteToken: string,
  names: { firstName: string; lastName: string },
  password: string = STAFF_PASSWORD
): Promise<{ result: ApiResult; staff: AcceptedStaff | null }> {
  const res = await request('POST', '/api/staff-invites/accept', {
    token: inviteToken,
    firstName: names.firstName,
    lastName: names.lastName,
    password,
  });

  if (res.success && res.data?.token) {
    return {
      result: res,
      staff: {
        token: res.data.token,
        userId: res.data.user.id,
        email: res.data.user.email,
        password,
      },
    };
  }
  return { result: res, staff: null };
}

// Convenience: invite + accept in one shot. Returns the new staff identity plus
// the staffProfileId (resolved from the admin's staff list).
export async function createStaffViaInvite(
  adminToken: string,
  opts: {
    orgRoleId: number;
    schoolId?: number | null;
    email?: string;
    firstName?: string;
    lastName?: string;
  }
): Promise<AcceptedStaff & { staffProfileId: number; orgRoleId: number }> {
  const invite = await inviteStaffViaApi(adminToken, {
    email: opts.email,
    orgRoleId: opts.orgRoleId,
    schoolId: opts.schoolId,
  });
  if (!invite.inviteToken) {
    throw new Error(`invite produced no token (ExposeLinksForTesting off?): ${JSON.stringify(invite.raw).substring(0, 300)}`);
  }

  const accepted = await acceptStaffInviteViaApi(
    invite.inviteToken,
    { firstName: opts.firstName ?? 'Staff', lastName: opts.lastName ?? 'Member' }
  );
  if (!accepted.staff) {
    throw new Error(`accept invite failed: ${JSON.stringify(accepted.result).substring(0, 300)}`);
  }

  const staffProfileId = await findStaffProfileId(adminToken, accepted.staff.email);
  return { ...accepted.staff, staffProfileId, orgRoleId: opts.orgRoleId };
}

// ---- staff list / management ------------------------------------------------

export async function getStaffListViaApi(token: string): Promise<ApiResult> {
  return request('GET', '/api/district/staff', undefined, token);
}

export async function findStaffProfileId(adminToken: string, email: string): Promise<number> {
  const res = await getStaffListViaApi(adminToken);
  const member = (res.data?.members ?? []).find(
    (m: any) => m.email.toLowerCase() === email.toLowerCase()
  );
  if (!member) throw new Error(`staff member ${email} not found in staff list`);
  return member.staffProfileId;
}

export async function deactivateStaffViaApi(adminToken: string, staffProfileId: number): Promise<ApiResult> {
  return request('POST', `/api/district/staff/${staffProfileId}/deactivate`, undefined, adminToken);
}

export async function reactivateStaffViaApi(adminToken: string, staffProfileId: number): Promise<ApiResult> {
  return request('POST', `/api/district/staff/${staffProfileId}/reactivate`, undefined, adminToken);
}

export async function revokeInviteViaApi(adminToken: string, inviteId: number): Promise<ApiResult> {
  return request('DELETE', `/api/district/staff/invites/${inviteId}`, undefined, adminToken);
}

// ---- students + staff-access ------------------------------------------------

export interface StudentRef {
  id: number;
  schoolId: number;
  firstName: string;
}

export async function createStudentViaApi(
  staffToken: string,
  opts: {
    firstName: string;
    lastName?: string;
    dateOfBirth?: string;
    schoolId?: number;
  }
): Promise<{ result: ApiResult; student: StudentRef | null }> {
  const body: any = {
    firstName: opts.firstName,
    lastName: opts.lastName ?? 'Student',
    dateOfBirth: opts.dateOfBirth ?? '2015-01-01',
  };
  if (opts.schoolId != null) body.schoolId = opts.schoolId;

  const res = await request('POST', '/api/educator/students', body, staffToken);
  if (res.success && res.data?.id) {
    return {
      result: res,
      student: { id: res.data.id, schoolId: res.data.schoolId, firstName: res.data.firstName },
    };
  }
  return { result: res, student: null };
}

export async function listStudentsViaApi(token: string): Promise<ApiResult> {
  return request('GET', '/api/educator/students', undefined, token);
}

export async function getStudentDetailStatus(token: string, studentId: number): Promise<number> {
  return rawStatus('GET', `/api/educator/students/${studentId}`, token);
}

export async function grantStaffAccessViaApi(
  adminToken: string,
  studentId: number,
  staffProfileId: number,
  accessRole?: string
): Promise<ApiResult> {
  const body: any = { staffProfileId };
  if (accessRole) body.accessRole = accessRole;
  return request('POST', `/api/educator/students/${studentId}/staff-access`, body, adminToken);
}

export async function getStaffAccessViaApi(adminToken: string, studentId: number): Promise<ApiResult> {
  return request('GET', `/api/educator/students/${studentId}/staff-access`, undefined, adminToken);
}

// Revoke the access grant belonging to a specific staff profile (the student's
// creator also holds an Owner grant, so we must target by staffProfileId).
export async function revokeStaffAccessForProfile(
  adminToken: string,
  studentId: number,
  staffProfileId: number
): Promise<ApiResult> {
  const list = await getStaffAccessViaApi(adminToken, studentId);
  const grant = (list.data ?? []).find((a: any) => a.staffProfileId === staffProfileId);
  if (!grant) throw new Error(`no access grant for staffProfileId ${staffProfileId} on student ${studentId}`);
  return request('DELETE', `/api/educator/students/${studentId}/staff-access/${grant.accessId}`, undefined, adminToken);
}

// ---- cleanup ----------------------------------------------------------------

// Deactivate a set of users via the platform-admin endpoint (existing teardown
// pattern). Best-effort: swallow failures so a flaky cleanup never fails a run.
export async function deactivateUsers(adminToken: string, userIds: number[]): Promise<void> {
  for (const id of userIds) {
    if (!id) continue;
    try {
      await apiPut(`/api/users/${id}`, { isActive: false }, adminToken);
    } catch {
      // ignore
    }
  }
}

// re-export apiGet for specs that only need a quick read.
export { apiGet };
