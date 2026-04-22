import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateEtrRequest,
  EtrDocument,
  UpdateEtrMetadataRequest,
} from '../types';

export async function listByChild(childId: number): Promise<ApiResponse<EtrDocument[]>> {
  const response = await apiClient.get<ApiResponse<EtrDocument[]>>(
    `/api/children/${childId}/etrs`
  );
  return response.data;
}

export async function getById(id: number): Promise<ApiResponse<EtrDocument>> {
  const response = await apiClient.get<ApiResponse<EtrDocument>>(`/api/etrs/${id}`);
  return response.data;
}

export async function create(
  childId: number,
  payload: CreateEtrRequest
): Promise<ApiResponse<EtrDocument>> {
  const response = await apiClient.post<ApiResponse<EtrDocument>>(
    `/api/children/${childId}/etrs`,
    payload
  );
  return response.data;
}

export async function updateMetadata(
  id: number,
  payload: UpdateEtrMetadataRequest
): Promise<ApiResponse<null>> {
  const response = await apiClient.put<ApiResponse<null>>(`/api/etrs/${id}/metadata`, payload);
  return response.data;
}

export async function remove(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(`/api/etrs/${id}`);
  return response.data;
}
