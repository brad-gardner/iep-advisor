import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { DraftResponseDto, DraftResponseStatus, SharedDraftRevisionDto } from '@/features/shared-drafts/types';
import type { ConvergeDto, RecipientPreviewDto, ResolveResponseRequest, ShareDraftRequest } from '../types';

// Thin async wrappers over apiClient for the staff draft-sharing endpoints
// (plan6-contract.md). Error responses reject as an AxiosError whose body is
// the ApiResponse envelope — callers catch with `apiErrorMessage`.

export async function getSharePreview(instanceId: number): Promise<ApiResponse<RecipientPreviewDto>> {
  const res = await apiClient.get<ApiResponse<RecipientPreviewDto>>(
    `/api/documents/${instanceId}/share/preview`
  );
  return res.data;
}

/**
 * Snapshot the current draft as a new shared revision (previous Active
 * revision → Superseded). 403 when the district has disabled family sharing;
 * 400 when the instance isn't Draft/Finalizing or has no recipients.
 */
export async function shareDraft(
  instanceId: number,
  request: ShareDraftRequest
): Promise<ApiResponse<SharedDraftRevisionDto>> {
  const res = await apiClient.post<ApiResponse<SharedDraftRevisionDto>>(
    `/api/documents/${instanceId}/share`,
    request
  );
  return res.data;
}

/** Every revision shared for this instance, newest first. */
export async function getShares(instanceId: number): Promise<ApiResponse<SharedDraftRevisionDto[]>> {
  const res = await apiClient.get<ApiResponse<SharedDraftRevisionDto[]>>(
    `/api/documents/${instanceId}/shares`
  );
  return res.data;
}

export async function withdrawShare(
  instanceId: number,
  revisionId: number
): Promise<ApiResponse<SharedDraftRevisionDto>> {
  const res = await apiClient.post<ApiResponse<SharedDraftRevisionDto>>(
    `/api/documents/${instanceId}/shares/${revisionId}/withdraw`,
    {}
  );
  return res.data;
}

export async function getConverge(instanceId: number): Promise<ApiResponse<ConvergeDto>> {
  const res = await apiClient.get<ApiResponse<ConvergeDto>>(`/api/documents/${instanceId}/converge`);
  return res.data;
}

export async function getInstanceResponses(
  instanceId: number,
  status: DraftResponseStatus | 'All' = 'All'
): Promise<ApiResponse<DraftResponseDto[]>> {
  const res = await apiClient.get<ApiResponse<DraftResponseDto[]>>(
    `/api/documents/${instanceId}/responses`,
    { params: { status } }
  );
  return res.data;
}

export async function resolveResponse(
  responseId: number,
  request: ResolveResponseRequest
): Promise<ApiResponse<DraftResponseDto>> {
  const res = await apiClient.post<ApiResponse<DraftResponseDto>>(
    `/api/responses/${responseId}/resolve`,
    request
  );
  return res.data;
}
