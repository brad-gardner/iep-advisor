import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';

export type ParentContributionKind = 'Strength' | 'Concern' | 'WorksAtHome' | 'Priority' | 'Other';

export const CONTRIBUTION_KINDS: ParentContributionKind[] = ['Strength', 'Concern', 'WorksAtHome', 'Priority', 'Other'];

// Display label moved to `@/lib/contribution-label.ts` (`contributionKindLabel`,
// translated via the eager `contributions` namespace — i18n plan phase 5).
// `CONTRIBUTION_KIND_LABELS`'s only caller (`about-my-child-card.tsx`) now
// uses that helper, so the old map was removed outright.

export interface ParentContributionDto {
  id: number;
  childProfileId: number;
  kind: ParentContributionKind;
  text: string;
  isShared: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SaveParentContributionRequest {
  kind: ParentContributionKind;
  text: string;
  isShared: boolean;
}

export async function listContributions(childId: number): Promise<ApiResponse<ParentContributionDto[]>> {
  const res = await apiClient.get<ApiResponse<ParentContributionDto[]>>(`/api/children/${childId}/contributions`);
  return res.data;
}

export async function createContribution(childId: number, body: SaveParentContributionRequest): Promise<ApiResponse<ParentContributionDto>> {
  const res = await apiClient.post<ApiResponse<ParentContributionDto>>(`/api/children/${childId}/contributions`, body);
  return res.data;
}

export async function updateContribution(id: number, body: SaveParentContributionRequest): Promise<ApiResponse<ParentContributionDto>> {
  const res = await apiClient.put<ApiResponse<ParentContributionDto>>(`/api/contributions/${id}`, body);
  return res.data;
}

export async function deleteContribution(id: number): Promise<ApiResponse<unknown>> {
  const res = await apiClient.delete<ApiResponse<unknown>>(`/api/contributions/${id}`);
  return res.data;
}

/** Staff: shared family notes for a school student. */
export async function listSharedContributionsForStudent(studentId: number): Promise<ApiResponse<ParentContributionDto[]>> {
  const res = await apiClient.get<ApiResponse<ParentContributionDto[]>>(`/api/educator/students/${studentId}/contributions`);
  return res.data;
}
