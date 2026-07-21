// DTOs for the admin document-template surface (Phase 1). Enums are serialized
// as strings on the backend and mirrored here as TS unions.

export interface DocumentTypeDto {
  id: number;
  key: string;
  displayName: string;
  isActive: boolean;
}

export type TemplateVersionStatus = 'Draft' | 'Published';

export interface TemplateVersionSummaryDto {
  id: number;
  versionNumber: number;
  status: TemplateVersionStatus;
  publishedAt: string | null;
}

export interface DocumentTemplateDto {
  id: number;
  /** 2-letter uppercase state code, or `null` for the default (all-states) template. */
  stateCode: string | null;
  documentTypeId: number;
  documentTypeKey: string;
  documentTypeDisplayName: string;
  name: string;
  createdAt: string;
  latestVersion: TemplateVersionSummaryDto | null;
}

export interface CreateTemplateRequest {
  /** Omit/undefined to create a default (all-states) template. */
  stateCode?: string;
  documentTypeId: number;
  name: string;
}
