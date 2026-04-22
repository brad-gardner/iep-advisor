import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateEtrRequest,
  EtrAnalysis,
  EtrDocument,
  EtrSection,
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

export async function uploadFile(
  id: number,
  file: File,
  onProgress?: (pct: number) => void
): Promise<ApiResponse<EtrDocument>> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<ApiResponse<EtrDocument>>(
    `/api/etrs/${id}/upload`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (event) => {
        if (!onProgress || !event.total) return;
        const pct = Math.round((event.loaded / event.total) * 100);
        onProgress(pct);
      },
    }
  );
  return response.data;
}

export async function getDownloadUrl(
  id: number
): Promise<ApiResponse<{ url: string }>> {
  const response = await apiClient.get<ApiResponse<{ url: string }>>(
    `/api/etrs/${id}/download`
  );
  return response.data;
}

export async function getSections(id: number): Promise<ApiResponse<EtrSection[]>> {
  const response = await apiClient.get<ApiResponse<EtrSection[]>>(
    `/api/etrs/${id}/sections`
  );
  return response.data;
}

export async function reprocess(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(`/api/etrs/${id}/process`);
  return response.data;
}

export async function startAnalysis(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(`/api/etrs/${id}/analyze`);
  return response.data;
}

export async function getAnalysis(id: number): Promise<ApiResponse<EtrAnalysis>> {
  const response = await apiClient.get<ApiResponse<EtrAnalysis>>(
    `/api/etrs/${id}/analysis`
  );
  return response.data;
}
