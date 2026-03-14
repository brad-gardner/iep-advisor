import { apiClient } from '@/lib/api-client';
import type { ApiResponse, TimelineResult, ComparisonResult } from '@/types/api';

export async function getTimeline(childId: number): Promise<ApiResponse<TimelineResult>> {
  const response = await apiClient.get<ApiResponse<TimelineResult>>(
    `/api/children/${childId}/iep-timeline`
  );
  return response.data;
}

export async function compareIeps(
  iepId: number,
  otherId: number
): Promise<ApiResponse<ComparisonResult>> {
  const response = await apiClient.get<ApiResponse<ComparisonResult>>(
    `/api/ieps/${iepId}/compare/${otherId}`
  );
  return response.data;
}
