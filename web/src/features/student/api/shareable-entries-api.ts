import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { StudentWorkspaceEntryDto } from '../types';

// Educator reads the shareable (only) entries a student chose to share with
// their team (P8a). Used to pull text into IEP authoring fields.
export async function getEducatorShareableEntries(
  studentId: number
): Promise<ApiResponse<StudentWorkspaceEntryDto[]>> {
  const response = await apiClient.get<ApiResponse<StudentWorkspaceEntryDto[]>>(
    `/api/educator/students/${studentId}/shareable-entries`
  );
  return response.data;
}

// Parent reads the shareable (only) entries their child chose to share, to
// inform meeting prep (P8a).
export async function getChildShareableEntries(
  childId: number
): Promise<ApiResponse<StudentWorkspaceEntryDto[]>> {
  const response = await apiClient.get<ApiResponse<StudentWorkspaceEntryDto[]>>(
    `/api/children/${childId}/shareable-entries`
  );
  return response.data;
}
