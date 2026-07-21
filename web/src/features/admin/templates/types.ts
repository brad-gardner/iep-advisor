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

// ---------------------------------------------------------------------------
// Phase 2 — template builder (author sections & fields, publish a version)
// ---------------------------------------------------------------------------

/** Field-type palette. Bound to render logic; a new type is a code change. */
export type FieldType = 'Text' | 'RichText' | 'Date' | 'Select' | 'Checkbox' | 'Table';

export interface TemplateFieldDto {
  id: number;
  /** Stable key; reordering/versioning never remaps stored values. */
  fieldKey: string;
  fieldType: FieldType;
  label: string;
  required: boolean;
  /** Stringified JSON whose shape depends on `fieldType` (see template-config.ts). */
  configJson: string | null;
  displayOrder: number;
}

export interface TemplateSectionDto {
  id: number;
  sectionKey: string;
  title: string;
  displayOrder: number;
  fields: TemplateFieldDto[];
}

export interface TemplateVersionDetailDto {
  id: number;
  documentTemplateId: number;
  versionNumber: number;
  status: TemplateVersionStatus;
  publishedAt: string | null;
  /** Base64 optimistic-concurrency token; thread back through every mutation. */
  rowVersion: string | null;
  sections: TemplateSectionDto[];
}

// --- Mutation request bodies (rowVersion threaded for optimistic concurrency) ---

export interface CreateSectionRequest {
  title: string;
  rowVersion?: string;
}

export interface UpdateSectionRequest {
  title: string;
  rowVersion?: string;
}

export interface ReorderRequest {
  orderedIds: number[];
  rowVersion?: string;
}

export interface CreateFieldRequest {
  fieldType: FieldType;
  label: string;
  required: boolean;
  configJson?: string;
  rowVersion?: string;
}

export interface UpdateFieldRequest {
  fieldType: FieldType;
  label: string;
  required: boolean;
  configJson?: string;
  rowVersion?: string;
}

export interface PublishRequest {
  rowVersion?: string;
}
