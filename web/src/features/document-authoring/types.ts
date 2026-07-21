// DTOs for the educator document-authoring surface (Phase 3). Enums are
// serialized as strings on the backend and mirrored here as TS unions. The
// template tree (sections/fields/config) is reused verbatim from the admin
// template surface — an instance pins one published TemplateVersionDetailDto.

export type {
  DocumentTypeDto,
  FieldType,
  TemplateFieldDto,
  TemplateSectionDto,
  TemplateVersionDetailDto,
} from '@/features/admin/templates/types';

import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

export type DocumentInstanceStatus = 'Draft' | 'Finalizing' | 'Finalized';

/** A single Table row: cells keyed by the field's Table `columnKey` (guid). */
export type TableCellValue = string | boolean;
export type TableRowValue = Record<string, TableCellValue>;

/** A value-document entry. Scalars for Text/RichText/Date/Select/Checkbox; an
 *  array of row objects for Table. */
export type DocumentFieldValue = string | boolean | TableRowValue[];

/** Patch of one-or-more field values, keyed by `FieldKey` (guid). */
export type DocumentValuePatch = Record<string, DocumentFieldValue>;

export interface DocumentInstanceDetailDto {
  id: number;
  schoolStudentId: number;
  documentTypeId: number;
  documentTypeKey: string;
  documentTypeDisplayName: string;
  documentTemplateVersionId: number;
  status: DocumentInstanceStatus;
  /** Keyed by `FieldKey` (guid). See DocumentFieldValue for per-type shapes. */
  values: Record<string, unknown>;
  /** Base64 optimistic-concurrency token; thread back through every save. */
  rowVersion: string | null;
  createdAt: string;
  lastEditedAt: string | null;
  lastEditedByUserId: number | null;
  templateVersion: TemplateVersionDetailDto;
}

export interface DocumentInstanceSummaryDto {
  id: number;
  documentTypeId: number;
  documentTypeKey: string;
  documentTypeDisplayName: string;
  status: DocumentInstanceStatus;
  documentTemplateVersionId: number;
  templateVersionNumber: number;
  createdAt: string;
  updatedAt: string;
  lastEditedAt: string | null;
}

export interface CreateDocumentRequest {
  documentTypeId: number;
}

export interface SaveValuesRequest {
  values: DocumentValuePatch;
  rowVersion?: string;
}

// ---------------------------------------------------------------------------
// Phase 4 — finalize → immutable authored version → dynamic PDF
// ---------------------------------------------------------------------------

/** PDF render lifecycle for a finalized authored version. */
export type PdfRenderStatus = 'Pending' | 'Rendered' | 'Error';

/** An immutable finalized version of an authored document (summary row). */
export interface AuthoredDocumentVersionSummaryDto {
  id: number;
  schoolStudentId: number;
  documentTypeId: number;
  documentTypeKey: string;
  documentTypeDisplayName: string;
  /** Per (student, documentType) — IEP and ETR number independently. */
  versionNumber: number;
  finalizedByUserId: number | null;
  finalizedAt: string;
  pdfRenderStatus: PdfRenderStatus | null;
}

/** Full frozen snapshot: the summary plus the pinned template tree + values. */
export interface AuthoredDocumentVersionDetailDto extends AuthoredDocumentVersionSummaryDto {
  documentTemplateVersionId: number;
  /** Frozen value-document keyed by `FieldKey` (guid). See DocumentFieldValue. */
  values: Record<string, unknown>;
  pdfBlobUri: string | null;
  pdfRenderedAt: string | null;
  /** The immutable template version this snapshot was rendered against. */
  templateVersion: TemplateVersionDetailDto;
}

/** PDF render status for a finalized version (url set only when Rendered). */
export interface AuthoredDocumentPdfStatusDto {
  versionId: number;
  renderStatus: PdfRenderStatus;
  url: string | null;
  renderedAt: string | null;
  errorMessage: string | null;
}
