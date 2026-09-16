import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { ObligationDto, ObligationStatus } from '../types';

export async function listMyObligations(status?: ObligationStatus): Promise<ApiResponse<ObligationDto[]>> {
  const response = await apiClient.get<ApiResponse<ObligationDto[]>>('/api/educator/obligations/mine', {
    params: status ? { status } : undefined,
  });
  return response.data;
}

export async function listStudentObligations(studentId: number): Promise<ApiResponse<ObligationDto[]>> {
  const response = await apiClient.get<ApiResponse<ObligationDto[]>>(
    `/api/educator/students/${studentId}/obligations`
  );
  return response.data;
}

export async function listDistrictObligations(
  params: { schoolId?: number; status?: ObligationStatus } = {}
): Promise<ApiResponse<ObligationDto[]>> {
  const query: Record<string, string> = {};
  if (params.schoolId != null) query.schoolId = String(params.schoolId);
  if (params.status) query.status = params.status;
  const response = await apiClient.get<ApiResponse<ObligationDto[]>>('/api/educator/obligations', {
    params: query,
  });
  return response.data;
}
