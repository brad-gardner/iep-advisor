import { apiClient } from '@/lib/api-client';
import type {
  ApiResponse,
  LoginRequest,
  LoginResponse,
  MfaSetupResponse,
  MfaVerifySetupResponse,
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
