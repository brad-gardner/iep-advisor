import { apiClient } from '@/lib/api-client';
import type { ApiResponse, ChildAccessEntry, CreateInviteRequest } from '@/types/api';

export async function createInvite(
  childId: number,
  data: CreateInviteRequest
): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(
    `/api/children/${childId}/share`,
    data
  );
  return response.data;
}

export async function getAccessList(
  childId: number
): Promise<ApiResponse<ChildAccessEntry[]>> {
  const response = await apiClient.get<ApiResponse<ChildAccessEntry[]>>(
    `/api/children/${childId}/access`
  );
  return response.data;
}

export async function revokeAccess(
  childId: number,
  accessId: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/children/${childId}/access/${accessId}`
  );
  return response.data;
}

export async function acceptInvite(
  token: string
): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(
    '/api/invites/accept',
    { token }
  );
  return response.data;
}
