import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { ExportJobDto, ExportJobIdDto } from '../types';

/** District admin: enqueue a whole-district export. */
export async function enqueueDistrictExport(): Promise<ApiResponse<ExportJobIdDto>> {
  const res = await apiClient.post<ApiResponse<ExportJobIdDto>>('/api/district/exports', {});
  return res.data;
}

/** District-scoped list of export jobs — both District- and Student-scope jobs. */
export async function listDistrictExports(): Promise<ApiResponse<ExportJobDto[]>> {
  const res = await apiClient.get<ApiResponse<ExportJobDto[]>>('/api/district/exports');
  return res.data;
}

/** Staff (Collaborator+): enqueue a single student's export. */
export async function enqueueStudentExport(studentId: number): Promise<ApiResponse<ExportJobIdDto>> {
  const res = await apiClient.post<ApiResponse<ExportJobIdDto>>(
    `/api/educator/students/${studentId}/export`,
    {}
  );
  return res.data;
}

export async function getExportStatus(jobId: number): Promise<ApiResponse<ExportJobDto>> {
  const res = await apiClient.get<ApiResponse<ExportJobDto>>(`/api/exports/${jobId}`);
  return res.data;
}

/** A fresh short-lived download URL (15 min) for a Completed export's ZIP. */
export async function getExportDownloadUrl(jobId: number): Promise<ApiResponse<{ url: string }>> {
  const res = await apiClient.get<ApiResponse<{ url: string }>>(`/api/exports/${jobId}/download`);
  return res.data;
}
