import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateGoalObservationRequest,
  CreateGoalRetirementRequest,
  GoalLineageDto,
  GoalObservationDto,
  GoalRecordDto,
  UpdateGoalStatusRequest,
} from '../types';

// Thin async wrappers over apiClient for the plan 7 goal-record endpoints
// (plan7-contract.md "Phase 1"). Error responses reject as an AxiosError whose
// body is the ApiResponse envelope — callers catch with `apiErrorMessage`.

/** Current goal records for a student (staff, Viewer+): latest per lineage,
 *  each with its last 12 observations and trajectory. */
export async function getStudentGoals(studentId: number): Promise<ApiResponse<GoalRecordDto[]>> {
  const res = await apiClient.get<ApiResponse<GoalRecordDto[]>>(
    `/api/educator/students/${studentId}/goals`
  );
  return res.data;
}

/** Every record for every lineage, newest record first per lineage. */
export async function getStudentGoalHistory(studentId: number): Promise<ApiResponse<GoalLineageDto[]>> {
  const res = await apiClient.get<ApiResponse<GoalLineageDto[]>>(
    `/api/educator/students/${studentId}/goals/history`
  );
  return res.data;
}

/** Same shape as the staff read, scoped to a parent's linked child (read-only). */
export async function getChildGoals(childId: number): Promise<ApiResponse<GoalRecordDto[]>> {
  const res = await apiClient.get<ApiResponse<GoalRecordDto[]>>(`/api/children/${childId}/goals`);
  return res.data;
}

/** Log one progress observation. `value` or `note` is required by the server. */
export async function addGoalObservation(
  goalRecordId: number,
  request: CreateGoalObservationRequest
): Promise<ApiResponse<GoalObservationDto>> {
  const res = await apiClient.post<ApiResponse<GoalObservationDto>>(
    `/api/goals/${goalRecordId}/observations`,
    request
  );
  return res.data;
}

/** Mark a goal Met/NotMet/Active. `reason` is required by the server for NotMet. */
export async function updateGoalStatus(
  goalRecordId: number,
  request: UpdateGoalStatusRequest
): Promise<ApiResponse<GoalRecordDto>> {
  const res = await apiClient.put<ApiResponse<GoalRecordDto>>(
    `/api/goals/${goalRecordId}/status`,
    request
  );
  return res.data;
}

/**
 * Record why a goal row is being removed from a Draft/Finalizing document,
 * BEFORE the row is actually removed from the field's value array — consumed
 * by the finalize projection as the retired record's `StatusReason`. The
 * endpoint returns 201 with no body (no `ApiResponse` envelope), so a
 * resolved promise is the success signal; a rejection carries the envelope
 * for `apiErrorMessage`.
 */
export async function recordGoalRetirement(
  instanceId: number,
  request: CreateGoalRetirementRequest
): Promise<void> {
  await apiClient.post(`/api/documents/${instanceId}/goal-retirements`, request);
}
