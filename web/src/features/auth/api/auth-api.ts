import axios from 'axios';
import { apiClient } from '@/lib/api-client';
import type {
  ApiResponse,
  LoginRequest,
  LoginResponse,
  MfaSetupResponse,
  MfaVerifySetupResponse,
  RegisterDistrictRequest,
  RegisterRequest,
  UpdateProfileRequest,
  User,
} from '@/types/api';

export async function login(data: LoginRequest): Promise<ApiResponse<LoginResponse>> {
  const response = await apiClient.post<ApiResponse<LoginResponse>>('/api/auth/login', data);
  return response.data;
}

export async function register(data: RegisterRequest): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/register', data);
  return response.data;
}

// District self-serve signup. Returns the same shape as login (JWT + user).
export async function registerDistrict(
  data: RegisterDistrictRequest
): Promise<ApiResponse<LoginResponse>> {
  const response = await apiClient.post<ApiResponse<LoginResponse>>('/api/auth/register-district', data);
  return response.data;
}

export async function getCurrentUser(): Promise<ApiResponse<User>> {
  const response = await apiClient.get<ApiResponse<User>>('/api/auth/me');
  return response.data;
}

export async function updateProfile(data: UpdateProfileRequest): Promise<ApiResponse<User>> {
  const response = await apiClient.put<ApiResponse<User>>('/api/auth/me', data);
  return response.data;
}

// MFA
export async function setupMfa(): Promise<ApiResponse<MfaSetupResponse>> {
  const response = await apiClient.post<ApiResponse<MfaSetupResponse>>('/api/auth/mfa/setup');
  return response.data;
}

export async function verifyMfaSetup(code: string): Promise<ApiResponse<MfaVerifySetupResponse>> {
  const response = await apiClient.post<ApiResponse<MfaVerifySetupResponse>>('/api/auth/mfa/verify-setup', { code });
  return response.data;
}

export async function verifyMfa(mfaPendingToken: string, code: string): Promise<ApiResponse<LoginResponse>> {
  const response = await apiClient.post<ApiResponse<LoginResponse>>('/api/auth/mfa/verify', { mfaPendingToken, code });
  return response.data;
}

export async function mfaRecovery(mfaPendingToken: string, recoveryCode: string): Promise<ApiResponse<LoginResponse>> {
  const response = await apiClient.post<ApiResponse<LoginResponse>>('/api/auth/mfa/recovery', { mfaPendingToken, recoveryCode });
  return response.data;
}

export async function disableMfa(password: string, code: string): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/mfa/disable', { password, code });
  return response.data;
}

// Password reset
export async function forgotPassword(email: string): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/forgot-password', { email });
  return response.data;
}

export async function resetPassword(token: string, newPassword: string): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/reset-password', { token, newPassword });
  return response.data;
}

// Account
export async function exportData(): Promise<ApiResponse<unknown>> {
  const response = await apiClient.get<ApiResponse<unknown>>('/api/auth/data-export');
  return response.data;
}

export async function deleteAccount(password: string, mfaCode?: string): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/delete-account', { password, mfaCode });
  return response.data;
}

export async function cancelDeletion(): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/cancel-deletion');
  return response.data;
}

// Pilot-gates plan, phase 2: the anonymous counterpart to `cancelDeletion` —
// usable from the signed link emailed at request time, even though the
// requesting account is deactivated (sessions revoked) the moment deletion is
// scheduled. Hits `AccountController`, not `AuthController`.
export async function cancelDeletionByToken(token: string): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/account/cancel-deletion', { token });
  return response.data;
}

// Magic link (pilot-gates plan, phase 3). Always 202s — the response never
// reveals whether the address is eligible.
export async function requestMagicLink(email: string): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/magic-link', { email });
  return response.data;
}

// Same response shape as `login` (an existing-MFA challenge or a full
// session), plus a district-requires-MFA-setup refusal unique to this
// endpoint — see `LoginResponse.mfaSetupRequired`.
//
// Deliberately bypasses `apiClient`: an invalid/expired token answers 401
// (`AuthController.ConsumeMagicLink`), unlike every other public-token
// endpoint in this app (reset-password, staff-invite accept both answer
// 400) — `apiClient`'s shared response interceptor treats ANY 401 as "this
// session died," clearing the stored token and hard-navigating to /login
// before the caller ever gets to show its own recovery UI. This endpoint is
// anonymous, so there's no Authorization header to lose by skipping it.
export async function consumeMagicLink(token: string): Promise<ApiResponse<LoginResponse>> {
  const response = await axios.post<ApiResponse<LoginResponse>>('/api/auth/magic-link/consume', { token });
  return response.data;
}

// Onboarding
export async function completeOnboarding(): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>('/api/auth/complete-onboarding');
  return response.data;
}
