import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { DocumentTypeDto } from '@/features/admin/templates/types';
import type {
  AuthoredDocumentPdfStatusDto,
  AuthoredDocumentVersionDetailDto,
  AuthoredDocumentVersionSummaryDto,
  DocumentInstanceDetailDto,
  DocumentInstanceSummaryDto,
  DocumentValuePatch,
} from '../types';

// Thin async wrappers over apiClient for the educator document-authoring
// endpoints. Each returns the ApiResponse<T> envelope on success. Error
// responses (422 no-template, 409 stale rowVersion, 403/404) reject as an
// AxiosError whose body is the same envelope — callers catch and surface it.

/** Active document types available to author (IEP, Section504, ETR, …). */
export async function listDocumentTypes(): Promise<ApiResponse<DocumentTypeDto[]>> {
  const res = await apiClient.get<ApiResponse<DocumentTypeDto[]>>('/api/document-types');
  return res.data;
}

/**
 * Create a document instance for a student. Resolves the student's
 * state+type template and pins its published version. **422** (with a message)
 * when no template is available — callers surface that message, not a crash.
 */
export async function createDocument(
  studentId: number,
  documentTypeId: number
): Promise<ApiResponse<DocumentInstanceDetailDto>> {
  const res = await apiClient.post<ApiResponse<DocumentInstanceDetailDto>>(
    `/api/educator/students/${studentId}/documents`,
    { documentTypeId }
  );
  return res.data;
}

export async function listDocuments(
  studentId: number
): Promise<ApiResponse<DocumentInstanceSummaryDto[]>> {
  const res = await apiClient.get<ApiResponse<DocumentInstanceSummaryDto[]>>(
    `/api/educator/students/${studentId}/documents`
  );
  return res.data;
}

export async function getDocument(
  instanceId: number
): Promise<ApiResponse<DocumentInstanceDetailDto>> {
  const res = await apiClient.get<ApiResponse<DocumentInstanceDetailDto>>(
    `/api/documents/${instanceId}`
  );
  return res.data;
}

/**
 * Save a partial patch of field values. The pinned schema validates types;
 * unknown keys are ignored server-side. Threads `rowVersion` for optimistic
 * concurrency — a 409 rejects with the envelope in the body.
 */
export async function saveValues(
  instanceId: number,
  values: DocumentValuePatch,
  rowVersion?: string
): Promise<ApiResponse<DocumentInstanceDetailDto>> {
  const res = await apiClient.put<ApiResponse<DocumentInstanceDetailDto>>(
    `/api/documents/${instanceId}/values`,
    { values, rowVersion }
  );
  return res.data;
}

export async function deleteDocument(instanceId: number): Promise<ApiResponse<null>> {
  const res = await apiClient.delete<ApiResponse<null>>(`/api/documents/${instanceId}`);
  return res.data;
}

// ---------------------------------------------------------------------------
// Phase 4 — finalize → immutable authored version → dynamic PDF
// ---------------------------------------------------------------------------

/**
 * Finalize a draft instance into an immutable authored version and enqueue its
 * PDF render. Returns the new version summary. The instance stays a `Draft`
 * (re-finalize creates the next version). Error bodies keep the ApiResponse
 * envelope: **422** carries a complete `errors[]` of missing-required/invalid
 * fields; **409** a state-conflict `message` (e.g. already finalizing); 403/404.
 */
export async function finalizeDocument(
  instanceId: number
): Promise<ApiResponse<AuthoredDocumentVersionSummaryDto>> {
  const res = await apiClient.post<ApiResponse<AuthoredDocumentVersionSummaryDto>>(
    `/api/documents/${instanceId}/finalize`,
    {}
  );
  return res.data;
}

/** Educator: finalized authored versions for a school student (newest first). */
export async function listAuthoredVersions(
  studentId: number
): Promise<ApiResponse<AuthoredDocumentVersionSummaryDto[]>> {
  const res = await apiClient.get<ApiResponse<AuthoredDocumentVersionSummaryDto[]>>(
    `/api/educator/students/${studentId}/authored-versions`
  );
  return res.data;
}

/** Full frozen snapshot of one authored version (pinned template + values). */
export async function getAuthoredVersion(
  versionId: number
): Promise<ApiResponse<AuthoredDocumentVersionDetailDto>> {
  const res = await apiClient.get<ApiResponse<AuthoredDocumentVersionDetailDto>>(
    `/api/authored-versions/${versionId}`
  );
  return res.data;
}

/** PDF render status for a finalized version (url set only when Rendered). */
export async function getAuthoredPdfStatus(
  versionId: number
): Promise<ApiResponse<AuthoredDocumentPdfStatusDto>> {
  const res = await apiClient.get<ApiResponse<AuthoredDocumentPdfStatusDto>>(
    `/api/authored-versions/${versionId}/pdf`
  );
  return res.data;
}

/** Re-enqueue a failed/pending PDF render (educator only); poll again after. */
export async function retryAuthoredPdf(
  versionId: number
): Promise<ApiResponse<AuthoredDocumentPdfStatusDto>> {
  const res = await apiClient.post<ApiResponse<AuthoredDocumentPdfStatusDto>>(
    `/api/authored-versions/${versionId}/pdf/retry`,
    {}
  );
  return res.data;
}
