import { apiClient } from '@/lib/api-client';
import type { ApiResponse, LoginResponse } from '@/types/api';
import type {
  AcceptStaffInviteRequest,
  CreateStaffInviteRequest,
  StaffInvite,
  StaffInvitePreview,
  StaffList,
} from '../types';

// ---------------------------------------------------------------- Authenticated (district staff)

export async function getStaffList(): Promise<ApiResponse<StaffList>> {
  const response = await apiClient.get<ApiResponse<StaffList>>('/api/district/staff');
  return response.data;
}

export async function createStaffInvite(
  data: CreateStaffInviteRequest
): Promise<ApiResponse<StaffInvite>> {
  const response = await apiClient.post<ApiResponse<StaffInvite>>(
    '/api/district/staff/invites',
    data
  );
  return response.data;
}

export async function revokeStaffInvite(inviteId: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/district/staff/invites/${inviteId}`
  );
  return response.data;
}

export async function resendStaffInvite(
  inviteId: number
): Promise<ApiResponse<StaffInvite>> {
  const response = await apiClient.post<ApiResponse<StaffInvite>>(
    `/api/district/staff/invites/${inviteId}/resend`
  );
  return response.data;
}

export async function deactivateStaff(
  staffProfileId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(
    `/api/district/staff/${staffProfileId}/deactivate`
  );
  return response.data;
}

export async function reactivateStaff(
  staffProfileId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(
    `/api/district/staff/${staffProfileId}/reactivate`
  );
  return response.data;
}

// ---------------------------------------------------------------- Anonymous (accept-invite)

export async function previewStaffInvite(
  token: string
): Promise<ApiResponse<StaffInvitePreview>> {
  const response = await apiClient.get<ApiResponse<StaffInvitePreview>>(
    '/api/staff-invites/preview',
    { params: { token } }
  );
  return response.data;
}

export async function acceptStaffInvite(
  data: AcceptStaffInviteRequest
): Promise<ApiResponse<LoginResponse>> {
  const response = await apiClient.post<ApiResponse<LoginResponse>>(
    '/api/staff-invites/accept',
    data
  );
  return response.data;
}
