import { apiClient } from '@/lib/api-client';
import type { ApiResponse, MeetingPrepChecklist, CheckItemRequest } from '@/types/api';

export async function generateFromGoals(childId: number): Promise<ApiResponse<MeetingPrepChecklist>> {
  const response = await apiClient.post<ApiResponse<MeetingPrepChecklist>>(
    `/api/children/${childId}/meeting-prep`
  );
  return response.data;
}

export async function generateFromIep(iepId: number): Promise<ApiResponse<MeetingPrepChecklist>> {
  const response = await apiClient.post<ApiResponse<MeetingPrepChecklist>>(
    `/api/ieps/${iepId}/meeting-prep`
  );
  return response.data;
}

export async function getChecklistsByChild(childId: number): Promise<ApiResponse<MeetingPrepChecklist[]>> {
  const response = await apiClient.get<ApiResponse<MeetingPrepChecklist[]>>(
    `/api/children/${childId}/meeting-prep`
  );
  return response.data;
}

export async function getChecklist(id: number): Promise<ApiResponse<MeetingPrepChecklist>> {
  const response = await apiClient.get<ApiResponse<MeetingPrepChecklist>>(
    `/api/meeting-prep/${id}`
  );
  return response.data;
}

export async function checkItem(id: number, request: CheckItemRequest): Promise<ApiResponse<null>> {
  const response = await apiClient.put<ApiResponse<null>>(
    `/api/meeting-prep/${id}/check`,
    request
  );
  return response.data;
}

export async function deleteChecklist(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/meeting-prep/${id}`
  );
  return response.data;
}
