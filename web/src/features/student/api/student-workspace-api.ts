import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateWorkspaceEntryRequest,
  InterviewSuggestionDto,
  StudentWorkspaceDto,
  StudentWorkspaceEntryDto,
  UpdateWorkspaceEntryRequest,
} from '../types';

// The authenticated student loads their own workspace (P8a). The backend
// returns 404 when the StudentWorkspace feature flag is off.
export async function getStudentWorkspace(): Promise<
  ApiResponse<StudentWorkspaceDto>
> {
  const response = await apiClient.get<ApiResponse<StudentWorkspaceDto>>(
    '/api/student-workspace'
  );
  return response.data;
}

export async function createWorkspaceEntry(
  request: CreateWorkspaceEntryRequest
): Promise<ApiResponse<StudentWorkspaceEntryDto>> {
  const response = await apiClient.post<ApiResponse<StudentWorkspaceEntryDto>>(
    '/api/student-workspace/entries',
    request
  );
  return response.data;
}

export async function updateWorkspaceEntry(
  id: number,
  request: UpdateWorkspaceEntryRequest
): Promise<ApiResponse<StudentWorkspaceEntryDto>> {
  const response = await apiClient.put<ApiResponse<StudentWorkspaceEntryDto>>(
    `/api/student-workspace/entries/${id}`,
    request
  );
  return response.data;
}

export async function deleteWorkspaceEntry(
  id: number
): Promise<ApiResponse<null>> {
  const response = await apiClient.delete<ApiResponse<null>>(
    `/api/student-workspace/entries/${id}`
  );
  return response.data;
}

// The AI interview takes a free-text prompt and returns a single suggested
// statement. It is NOT persisted — the student chooses to save it as an entry.
export async function requestInterviewSuggestion(
  prompt: string
): Promise<ApiResponse<InterviewSuggestionDto>> {
  const response = await apiClient.post<ApiResponse<InterviewSuggestionDto>>(
    '/api/student-workspace/interview',
    { prompt }
  );
  return response.data;
}
