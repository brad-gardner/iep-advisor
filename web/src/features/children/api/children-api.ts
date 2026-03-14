import { apiClient } from '@/lib/api-client';
import type {
  ApiResponse,
  ChildProfile,
  CreateChildProfileRequest,
  UpdateChildProfileRequest,
} from '@/types/api';

export async function getChildren(): Promise<ApiResponse<ChildProfile[]>> {
  const response = await apiClient.get<ApiResponse<ChildProfile[]>>('/api/children');
  return response.data;
}

export async function getChild(id: number): Promise<ApiResponse<ChildProfile>> {
  const response = await apiClient.get<ApiResponse<ChildProfile>>(`/api/children/${id}`);
  return response.data;
}

export async function createChild(
  data: CreateChildProfileRequest
): Promise<ApiResponse<ChildProfile>> {
  const response = await apiClient.post<ApiResponse<ChildProfile>>('/api/children', data);
  return response.data;
}

export async function updateChild(
  id: number,
  data: UpdateChildProfileRequest
): Promise<ApiResponse<null>> {
  const response = await apiClient.put<ApiResponse<null>>(`/api/children/${id}`, data);
  return response.data;
}

export async function deleteChild(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(`/api/children/${id}`);
  return response.data;
}
