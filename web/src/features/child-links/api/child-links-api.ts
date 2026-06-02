import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  AcceptedChildLink,
  ChildLinkInvitePreview,
  ChildSchoolLink,
} from '../types';

export async function previewLink(
  token: string
): Promise<ApiResponse<ChildLinkInvitePreview>> {
  const response = await apiClient.get<ApiResponse<ChildLinkInvitePreview>>(
    '/api/child-links/preview',
    { params: { token } }
  );
  return response.data;
}

export async function acceptLink(
  token: string,
  linkToChildProfileId?: number
): Promise<ApiResponse<AcceptedChildLink>> {
  const response = await apiClient.post<ApiResponse<AcceptedChildLink>>(
    '/api/child-links/accept',
    { token, linkToChildProfileId }
  );
  return response.data;
}

export async function getChildSchoolLinks(
  childId: number
): Promise<ApiResponse<ChildSchoolLink[]>> {
  const response = await apiClient.get<ApiResponse<ChildSchoolLink[]>>(
    `/api/children/${childId}/school-links`
  );
  return response.data;
}
