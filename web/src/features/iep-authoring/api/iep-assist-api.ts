import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AssistKind,
  AssistResponse,
  ChatMessage,
  ChatResponse,
} from './iep-assist-types';

// ---- Inline field assist (per child row) ----

export async function assistGoal(
  draftId: number,
  goalId: number,
  kind: AssistKind
): Promise<ApiResponse<AssistResponse>> {
  const res = await apiClient.post<ApiResponse<AssistResponse>>(
    `/api/iep-drafts/${draftId}/goals/${goalId}/assist`,
    { kind }
  );
  return res.data;
}

export async function assistSection(
  draftId: number,
  sectionId: number,
  kind: AssistKind
): Promise<ApiResponse<AssistResponse>> {
  const res = await apiClient.post<ApiResponse<AssistResponse>>(
    `/api/iep-drafts/${draftId}/sections/${sectionId}/assist`,
    { kind }
  );
  return res.data;
}

export async function assistServiceLine(
  draftId: number,
  serviceLineId: number,
  kind: AssistKind
): Promise<ApiResponse<AssistResponse>> {
  const res = await apiClient.post<ApiResponse<AssistResponse>>(
    `/api/iep-drafts/${draftId}/service-lines/${serviceLineId}/assist`,
    { kind }
  );
  return res.data;
}

// ---- IEP-scoped sidebar chat ----

export async function chat(
  draftId: number,
  messages: ChatMessage[]
): Promise<ApiResponse<ChatResponse>> {
  const res = await apiClient.post<ApiResponse<ChatResponse>>(
    `/api/iep-drafts/${draftId}/chat`,
    { messages }
  );
  return res.data;
}
