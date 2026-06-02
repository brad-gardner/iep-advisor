import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  FinalizeIepDraftRequest,
  IepVersionDto,
  IepVersionPdfStatusDto,
  IepVersionSummaryDto,
} from '../types';

// Finalize a draft into an immutable version (educator). Returns the new summary.
export async function finalizeDraft(
  draftId: number,
  data: FinalizeIepDraftRequest
): Promise<ApiResponse<IepVersionSummaryDto>> {
  const res = await apiClient.post<ApiResponse<IepVersionSummaryDto>>(
    `/api/iep-drafts/${draftId}/finalize`,
    data
  );
  return res.data;
}

// Educator: versions for a school student (newest version first).
export async function listVersionsForStudent(
  studentId: number
): Promise<ApiResponse<IepVersionSummaryDto[]>> {
  const res = await apiClient.get<ApiResponse<IepVersionSummaryDto[]>>(
    `/api/educator/students/${studentId}/iep-versions`
  );
  return res.data;
}

// Parent: versions for the SchoolStudent linked to this child (newest first).
export async function listVersionsForChild(
  childId: number
): Promise<ApiResponse<IepVersionSummaryDto[]>> {
  const res = await apiClient.get<ApiResponse<IepVersionSummaryDto[]>>(
    `/api/children/${childId}/iep-versions`
  );
  return res.data;
}

// Full version snapshot (educator with access OR linked parent).
export async function getVersion(
  versionId: number
): Promise<ApiResponse<IepVersionDto>> {
  const res = await apiClient.get<ApiResponse<IepVersionDto>>(
    `/api/iep-versions/${versionId}`
  );
  return res.data;
}

// PDF render status (url set only when Rendered).
export async function getPdfStatus(
  versionId: number
): Promise<ApiResponse<IepVersionPdfStatusDto>> {
  const res = await apiClient.get<ApiResponse<IepVersionPdfStatusDto>>(
    `/api/iep-versions/${versionId}/pdf`
  );
  return res.data;
}

// Re-render an errored/pending PDF (educator only).
export async function retryPdf(
  versionId: number
): Promise<ApiResponse<IepVersionPdfStatusDto>> {
  const res = await apiClient.post<ApiResponse<IepVersionPdfStatusDto>>(
    `/api/iep-versions/${versionId}/pdf/retry`,
    {}
  );
  return res.data;
}
