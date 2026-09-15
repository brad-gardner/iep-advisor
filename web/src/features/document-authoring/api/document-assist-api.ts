import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { AssistKind, AssistResponse, ChatMessage, ChatResponse } from './assist-types';

/** Inline assist for one template field; Table fields also need the row's `_rowId`. */
export async function assistField(
  instanceId: number,
  fieldKey: string,
  rowId: string | null,
  kind: AssistKind
): Promise<ApiResponse<AssistResponse>> {
  const res = await apiClient.post<ApiResponse<AssistResponse>>(`/api/documents/${instanceId}/assist`, {
    fieldKey,
    rowId,
    kind,
  });
  return res.data;
}

/** Document-scoped assistant chat (ephemeral; the client owns the thread). */
export async function chat(instanceId: number, messages: ChatMessage[]): Promise<ApiResponse<ChatResponse>> {
  const res = await apiClient.post<ApiResponse<ChatResponse>>(`/api/documents/${instanceId}/chat`, { messages });
  return res.data;
}
