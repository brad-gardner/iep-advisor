import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { DocumentTypeDto } from '@/features/admin/templates/types';
import type {
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
