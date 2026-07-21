import { apiClient } from '@/lib/api-client';
import type { ApiResponse } from '@/types/api';
import type { CreateTemplateRequest, DocumentTemplateDto, DocumentTypeDto } from './types';

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
