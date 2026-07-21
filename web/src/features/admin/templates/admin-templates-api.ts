import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type {
  CreateFieldRequest,
  CreateSectionRequest,
  CreateTemplateRequest,
  DocumentTemplateDto,
  DocumentTypeDto,
  PublishRequest,
  ReorderRequest,
  TemplateVersionDetailDto,
  UpdateFieldRequest,
  UpdateSectionRequest,
} from './types';

// Thin async wrappers over apiClient for the Admin document-template endpoints.
// Each returns the ApiResponse<T> envelope so callers can surface the backend
// message (e.g. duplicate (state, type)) on a 400.

export async function listDocumentTypes(): Promise<ApiResponse<DocumentTypeDto[]>> {
  const res = await apiClient.get<ApiResponse<DocumentTypeDto[]>>('/api/admin/document-types');
  return res.data;
}

export async function listTemplates(): Promise<ApiResponse<DocumentTemplateDto[]>> {
  const res = await apiClient.get<ApiResponse<DocumentTemplateDto[]>>('/api/admin/document-templates');
  return res.data;
}

export async function createTemplate(
  data: CreateTemplateRequest
): Promise<ApiResponse<DocumentTemplateDto>> {
  const res = await apiClient.post<ApiResponse<DocumentTemplateDto>>(
    '/api/admin/document-templates',
    data
  );
  return res.data;
}

// ---------------------------------------------------------------------------
// Phase 2 — template authoring. Every mutation returns the refreshed full
// version tree + a fresh base64 rowVersion in the ApiResponse envelope. A 409
// (stale rowVersion) rejects with the envelope in the body; callers surface a
// "reload" prompt.
// ---------------------------------------------------------------------------

type VersionResponse = Promise<ApiResponse<TemplateVersionDetailDto>>;

export async function getTemplateVersion(versionId: number): VersionResponse {
  const res = await apiClient.get<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-versions/${versionId}`
  );
  return res.data;
}

export async function createSection(versionId: number, body: CreateSectionRequest): VersionResponse {
  const res = await apiClient.post<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-versions/${versionId}/sections`,
    body
  );
  return res.data;
}

export async function updateSection(sectionId: number, body: UpdateSectionRequest): VersionResponse {
  const res = await apiClient.put<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-sections/${sectionId}`,
    body
  );
  return res.data;
}

export async function deleteSection(sectionId: number, rowVersion: string | null): VersionResponse {
  const res = await apiClient.delete<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-sections/${sectionId}`,
    { params: rowVersion ? { rowVersion } : undefined }
  );
  return res.data;
}

export async function reorderSections(versionId: number, body: ReorderRequest): VersionResponse {
  const res = await apiClient.put<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-versions/${versionId}/sections/order`,
    body
  );
  return res.data;
}

export async function createField(sectionId: number, body: CreateFieldRequest): VersionResponse {
  const res = await apiClient.post<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-sections/${sectionId}/fields`,
    body
  );
  return res.data;
}

export async function updateField(fieldId: number, body: UpdateFieldRequest): VersionResponse {
  const res = await apiClient.put<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-fields/${fieldId}`,
    body
  );
  return res.data;
}

export async function deleteField(fieldId: number, rowVersion: string | null): VersionResponse {
  const res = await apiClient.delete<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-fields/${fieldId}`,
    { params: rowVersion ? { rowVersion } : undefined }
  );
  return res.data;
}

export async function reorderFields(sectionId: number, body: ReorderRequest): VersionResponse {
  const res = await apiClient.put<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-template-sections/${sectionId}/fields/order`,
    body
  );
  return res.data;
}

export async function publishTemplate(templateId: number, body: PublishRequest): VersionResponse {
  const res = await apiClient.post<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-templates/${templateId}/publish`,
    body
  );
  return res.data;
}

export async function createDraft(templateId: number): VersionResponse {
  const res = await apiClient.post<ApiResponse<TemplateVersionDetailDto>>(
    `/api/admin/document-templates/${templateId}/create-draft`
  );
  return res.data;
}
