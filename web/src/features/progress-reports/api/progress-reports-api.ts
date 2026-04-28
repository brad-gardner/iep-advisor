import { apiClient } from "@/lib/api-client";
import type { ApiResponse } from "@/types/api";
import type {
  CreateProgressReportRequest,
  ProgressReport,
} from "../types";

export async function listByIep(
  iepId: number
): Promise<ApiResponse<ProgressReport[]>> {
  const response = await apiClient.get<ApiResponse<ProgressReport[]>>(
    `/api/ieps/${iepId}/progress-reports`
  );
  return response.data;
}

export async function getById(id: number): Promise<ApiResponse<ProgressReport>> {
  const response = await apiClient.get<ApiResponse<ProgressReport>>(
    `/api/progress-reports/${id}`
  );
  return response.data;
}

export async function create(
  iepId: number,
  data: CreateProgressReportRequest
): Promise<ApiResponse<ProgressReport>> {
  const response = await apiClient.post<ApiResponse<ProgressReport>>(
    `/api/ieps/${iepId}/progress-reports`,
    data
  );
  return response.data;
}

export async function uploadFile(
  id: number,
  file: File
): Promise<ApiResponse<ProgressReport>> {
  const formData = new FormData();
  formData.append("file", file);
  const response = await apiClient.post<ApiResponse<ProgressReport>>(
    `/api/progress-reports/${id}/upload`,
    formData,
    { headers: { "Content-Type": "multipart/form-data" } }
  );
  return response.data;
}

export async function updateMetadata(
  id: number,
  data: CreateProgressReportRequest
): Promise<ApiResponse<null>> {
  const response = await apiClient.put<ApiResponse<null>>(
    `/api/progress-reports/${id}`,
    data
  );
  return response.data;
}

export async function getDownloadUrl(
  id: number
): Promise<ApiResponse<{ url: string }>> {
  const response = await apiClient.get<ApiResponse<{ url: string }>>(
    `/api/progress-reports/${id}/download`
  );
  return response.data;
}

export async function remove(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/progress-reports/${id}`
  );
  return response.data;
}
