import { apiClient } from '@/lib/api-client';
import type { ApiResponse, IepAnalysis, IepDocument, IepSection } from '@/types/api';

export async function getIepDocuments(childId: number): Promise<ApiResponse<IepDocument[]>> {
  const response = await apiClient.get<ApiResponse<IepDocument[]>>(
    `/api/children/${childId}/ieps`
  );
  return response.data;
}

export async function getIepDocument(id: number): Promise<ApiResponse<IepDocument>> {
  const response = await apiClient.get<ApiResponse<IepDocument>>(`/api/ieps/${id}`);
  return response.data;
}

export async function uploadIepDocument(
  childId: number,
  file: File
): Promise<ApiResponse<IepDocument>> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<ApiResponse<IepDocument>>(
    `/api/children/${childId}/ieps`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );
  return response.data;
}

export async function getDownloadUrl(id: number): Promise<ApiResponse<{ url: string }>> {
  const response = await apiClient.get<ApiResponse<{ url: string }>>(`/api/ieps/${id}/download`);
  return response.data;
}

export async function deleteIepDocument(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(`/api/ieps/${id}`);
  return response.data;
}

export async function getIepSections(documentId: number): Promise<ApiResponse<IepSection[]>> {
  const response = await apiClient.get<ApiResponse<IepSection[]>>(
    `/api/ieps/${documentId}/sections`
  );
  return response.data;
}

export async function reprocessIep(documentId: number): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(`/api/ieps/${documentId}/process`);
  return response.data;
}

export async function triggerAnalysis(documentId: number): Promise<ApiResponse<null>> {
  const response = await apiClient.post<ApiResponse<null>>(`/api/ieps/${documentId}/analyze`);
  return response.data;
}

export async function getAnalysis(documentId: number): Promise<ApiResponse<IepAnalysis>> {
  const response = await apiClient.get<ApiResponse<IepAnalysis>>(
    `/api/ieps/${documentId}/analysis`
  );
  return response.data;
}
