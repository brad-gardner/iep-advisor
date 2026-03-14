import { apiClient } from '@/lib/api-client';
import type {
  ApiResponse,
  AdvocacyGoal,
  CreateAdvocacyGoalRequest,
  UpdateAdvocacyGoalRequest,
  ReorderAdvocacyGoalsRequest,
} from '@/types/api';

export async function getAdvocacyGoals(childId: number): Promise<ApiResponse<AdvocacyGoal[]>> {
  const response = await apiClient.get<ApiResponse<AdvocacyGoal[]>>(
    `/api/children/${childId}/advocacy-goals`
  );
  return response.data;
}

export async function createAdvocacyGoal(
  childId: number,
  data: CreateAdvocacyGoalRequest
): Promise<ApiResponse<AdvocacyGoal>> {
  const response = await apiClient.post<ApiResponse<AdvocacyGoal>>(
    `/api/children/${childId}/advocacy-goals`,
    data
  );
  return response.data;
}

export async function updateAdvocacyGoal(
  id: number,
  data: UpdateAdvocacyGoalRequest
): Promise<ApiResponse<null>> {
  const response = await apiClient.put<ApiResponse<null>>(`/api/advocacy-goals/${id}`, data);
  return response.data;
}

export async function deleteAdvocacyGoal(id: number): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(`/api/advocacy-goals/${id}`);
  return response.data;
}

export async function reorderAdvocacyGoals(
  childId: number,
  data: ReorderAdvocacyGoalsRequest
): Promise<ApiResponse<null>> {
  const response = await apiClient.put<ApiResponse<null>>(
    `/api/children/${childId}/advocacy-goals/reorder`,
    data
  );
  return response.data;
}
