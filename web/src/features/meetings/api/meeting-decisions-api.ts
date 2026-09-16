import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateMeetingDecisionRequest,
  MeetingDecisionDto,
  ProposedEditDto,
  UpdateMeetingDecisionRequest,
} from '../types';

// Thin async wrappers over apiClient for the plan 7 meeting-decisions /
// proposed-edits endpoints (plan7-contract.md "Phase 3"). Three route
// prefixes on purpose, mirroring the backend controller.

export async function getDecisions(meetingId: number): Promise<ApiResponse<MeetingDecisionDto[]>> {
  const res = await apiClient.get<ApiResponse<MeetingDecisionDto[]>>(`/api/meetings/${meetingId}/decisions`);
  return res.data;
}

export async function createDecision(
  meetingId: number,
  request: CreateMeetingDecisionRequest
): Promise<ApiResponse<MeetingDecisionDto>> {
  const res = await apiClient.post<ApiResponse<MeetingDecisionDto>>(
    `/api/meetings/${meetingId}/decisions`,
    request
  );
  return res.data;
}

export async function updateDecision(
  decisionId: number,
  request: UpdateMeetingDecisionRequest
): Promise<ApiResponse<MeetingDecisionDto>> {
  const res = await apiClient.put<ApiResponse<MeetingDecisionDto>>(`/api/decisions/${decisionId}`, request);
  return res.data;
}

/** 204 No Content on success — a resolved promise is the success signal; a
 *  rejection carries the ApiResponse envelope for `apiErrorMessage`. */
export async function deleteDecision(decisionId: number): Promise<void> {
  await apiClient.delete(`/api/decisions/${decisionId}`);
}

export async function getProposedEdits(instanceId: number): Promise<ApiResponse<ProposedEditDto[]>> {
  const res = await apiClient.get<ApiResponse<ProposedEditDto[]>>(`/api/documents/${instanceId}/proposed-edits`);
  return res.data;
}

export async function markDecisionApplied(decisionId: number): Promise<ApiResponse<ProposedEditDto>> {
  const res = await apiClient.post<ApiResponse<ProposedEditDto>>(`/api/decisions/${decisionId}/mark-applied`, {});
  return res.data;
}
