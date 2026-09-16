import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateEvaluationCaseRequest,
  CreateEvaluatorAssignmentRequest,
  CreateIepResponseDto,
  DetermineEvaluationRequest,
  EvaluationCaseDto,
  EvaluatorAssignmentDto,
  OverrideDueDateRequest,
  UpdateEvaluatorAssignmentRequest,
} from '../types';

// Thin async wrappers over apiClient for the plan 7 evaluation-case endpoints
// (plan7-contract.md "Phase 2"). Error responses reject as an AxiosError whose
// body is the ApiResponse envelope — callers catch with `apiErrorMessage`.

function base(studentId: number): string {
  return `/api/educator/students/${studentId}/evaluation`;
}

/** The student's open case, else their most recent closed one, else null. */
export async function getEvaluationCase(studentId: number): Promise<ApiResponse<EvaluationCaseDto | null>> {
  const res = await apiClient.get<ApiResponse<EvaluationCaseDto | null>>(base(studentId));
  return res.data;
}

export async function createEvaluationCase(
  studentId: number,
  request: CreateEvaluationCaseRequest
): Promise<ApiResponse<EvaluationCaseDto>> {
  const res = await apiClient.post<ApiResponse<EvaluationCaseDto>>(base(studentId), request);
  return res.data;
}

export async function requestConsent(
  studentId: number,
  requestedAt?: string
): Promise<ApiResponse<EvaluationCaseDto>> {
  const res = await apiClient.post<ApiResponse<EvaluationCaseDto>>(`${base(studentId)}/consent/request`, {
    requestedAt,
  });
  return res.data;
}

/**
 * Record consent as received, with an optional PDF (multipart when a file is
 * given, otherwise a plain JSON body — the controller accepts either).
 */
export async function receiveConsent(
  studentId: number,
  receivedAt: string,
  file?: File
): Promise<ApiResponse<EvaluationCaseDto>> {
  if (file) {
    const formData = new FormData();
    formData.append('receivedAt', receivedAt);
    formData.append('file', file);
    const res = await apiClient.post<ApiResponse<EvaluationCaseDto>>(
      `${base(studentId)}/consent/receive`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
    return res.data;
  }
  const res = await apiClient.post<ApiResponse<EvaluationCaseDto>>(`${base(studentId)}/consent/receive`, {
    receivedAt,
  });
  return res.data;
}

/** A fresh short-lived URL for the uploaded consent PDF. */
export async function getConsentDownloadUrl(studentId: number): Promise<ApiResponse<string>> {
  const res = await apiClient.get<ApiResponse<string>>(`${base(studentId)}/consent/download`);
  return res.data;
}

export async function overrideDueDate(
  studentId: number,
  request: OverrideDueDateRequest
): Promise<ApiResponse<EvaluationCaseDto>> {
  const res = await apiClient.put<ApiResponse<EvaluationCaseDto>>(`${base(studentId)}/due-date`, request);
  return res.data;
}

export async function addEvaluatorAssignment(
  studentId: number,
  request: CreateEvaluatorAssignmentRequest
): Promise<ApiResponse<EvaluatorAssignmentDto>> {
  const res = await apiClient.post<ApiResponse<EvaluatorAssignmentDto>>(
    `${base(studentId)}/assignments`,
    request
  );
  return res.data;
}

export async function updateEvaluatorAssignment(
  studentId: number,
  assignmentId: number,
  request: UpdateEvaluatorAssignmentRequest
): Promise<ApiResponse<EvaluatorAssignmentDto>> {
  const res = await apiClient.put<ApiResponse<EvaluatorAssignmentDto>>(
    `${base(studentId)}/assignments/${assignmentId}`,
    request
  );
  return res.data;
}

/** The endpoint returns 204 with no body — a resolved promise is the success
 *  signal; a rejection carries the `ApiResponse` envelope for `apiErrorMessage`. */
export async function removeEvaluatorAssignment(studentId: number, assignmentId: number): Promise<void> {
  await apiClient.delete(`${base(studentId)}/assignments/${assignmentId}`);
}

export async function determineEvaluation(
  studentId: number,
  request: DetermineEvaluationRequest
): Promise<ApiResponse<EvaluationCaseDto>> {
  const res = await apiClient.post<ApiResponse<EvaluationCaseDto>>(`${base(studentId)}/determine`, request);
  return res.data;
}

export async function closeEvaluation(studentId: number): Promise<ApiResponse<EvaluationCaseDto>> {
  const res = await apiClient.post<ApiResponse<EvaluationCaseDto>>(`${base(studentId)}/close`, {});
  return res.data;
}

export async function createIepFromEtr(studentId: number): Promise<ApiResponse<CreateIepResponseDto>> {
  const res = await apiClient.post<ApiResponse<CreateIepResponseDto>>(`${base(studentId)}/create-iep`, {});
  return res.data;
}
