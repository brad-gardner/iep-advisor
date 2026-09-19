import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';

/** Who wrote the question: the parent themselves, or accepted from an advocate suggestion. */
export type PrepQuestionSource = 'parent' | 'advocate';

export interface ParentPrepQuestionDto {
  id: number;
  childProfileId: number;
  text: string;
  isChecked: boolean;
  displayOrder: number;
  source: PrepQuestionSource;
  createdAt: string;
  updatedAt: string;
  /** Set by a create that matched an existing question (case-insensitive) — the match is returned instead. */
  alreadyExisted?: boolean;
}

export interface CreatePrepQuestionRequest {
  text: string;
  source?: PrepQuestionSource;
}

export interface UpdatePrepQuestionRequest {
  text?: string;
  isChecked?: boolean;
}

export async function listPrepQuestions(childId: number): Promise<ApiResponse<ParentPrepQuestionDto[]>> {
  const res = await apiClient.get<ApiResponse<ParentPrepQuestionDto[]>>(`/api/children/${childId}/prep-questions`);
  return res.data;
}

export async function createPrepQuestion(childId: number, body: CreatePrepQuestionRequest): Promise<ApiResponse<ParentPrepQuestionDto>> {
  const res = await apiClient.post<ApiResponse<ParentPrepQuestionDto>>(`/api/children/${childId}/prep-questions`, body);
  return res.data;
}

export async function updatePrepQuestion(id: number, body: UpdatePrepQuestionRequest): Promise<ApiResponse<ParentPrepQuestionDto>> {
  const res = await apiClient.put<ApiResponse<ParentPrepQuestionDto>>(`/api/prep-questions/${id}`, body);
  return res.data;
}

/** Persists the full order of the child's questions; `ids` is every question id, first to last. */
export async function reorderPrepQuestions(childId: number, ids: number[]): Promise<ApiResponse<unknown>> {
  const res = await apiClient.put<ApiResponse<unknown>>(`/api/children/${childId}/prep-questions/order`, { ids });
  return res.data;
}

export async function deletePrepQuestion(id: number): Promise<ApiResponse<unknown>> {
  const res = await apiClient.delete<ApiResponse<unknown>>(`/api/prep-questions/${id}`);
  return res.data;
}
