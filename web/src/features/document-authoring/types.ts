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

/** A single Table row: cells keyed by the field's Table `columnKey` (guid), plus
 *  reserved `_rowId` / `_carriedFrom` / `_confirmed` metadata entries. */
export type TableCellValue = string | boolean | Record<string, unknown>;
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

  // Plan 7, decision 5: present when this draft amends a finalized version.
  amendsVersionId: number | null;
  amendsVersionNumber: number | null;
  amendmentReason: string | null;
  effectiveDate: string | null;

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

/** Lightweight save response: normalized values + rotated token (no template tree). */
export interface DocumentValuesResponseDto {
  /** Keyed by `FieldKey` (guid); normalized server-side (e.g. sanitized RichText). */
  values: Record<string, unknown>;
  rowVersion: string | null;
}

// ---------------------------------------------------------------------------
// Phase 4 — finalize → immutable authored version → dynamic PDF
// ---------------------------------------------------------------------------

/** PDF render lifecycle for a finalized authored version. */
export type PdfRenderStatus = 'Pending' | 'Rendered' | 'Error';

// Plan 7, decision 4: print/sign status of a finalized version. Typed-name
// e-sign is explicitly not claimed — this reflects whether a scanned/uploaded
// signed copy has been attached.
export const SIGNATURE_STATUSES = ['Unsigned', 'PartiallySigned', 'Signed'] as const;
export type SignatureStatus = (typeof SIGNATURE_STATUSES)[number];
export const SIGNATURE_STATUS_LABELS: Record<SignatureStatus, string> = {
  Unsigned: 'Unsigned',
  PartiallySigned: 'Partially signed',
  Signed: 'Signed',
};

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

  // Plan 7, decisions 4-5: signatures + amendment chain.
  signatureStatus: SignatureStatus;
  signedArtifactCount: number;
  amendsVersionId: number | null;
  amendsVersionNumber: number | null;
  amendmentReason: string | null;
  effectiveDate: string | null;
  amendedByVersionIds: number[];
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

/** PDF render status for a finalized version. No URL — poll this, then fetch the
 *  download URL separately (that call is what records the FERPA Export audit). */
export interface AuthoredDocumentPdfStatusDto {
  versionId: number;
  renderStatus: PdfRenderStatus;
  renderedAt: string | null;
  errorMessage: string | null;
}

/** A freshly-minted short-lived download URL (from the download endpoint). */
export interface AuthoredDocumentPdfDownloadDto {
  url: string;
}

// ---------------------------------------------------------------------------
// Plan 7 — amendments + signed artifacts
// ---------------------------------------------------------------------------

export interface AmendDocumentVersionRequest {
  reason: string;
  effectiveDate?: string;
}

export interface AmendResultDto {
  instanceId: number;
}

export interface SignedArtifactDto {
  id: number;
  authoredDocumentVersionId: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: number;
  uploadedByName: string | null;
  uploadedAt: string;
  signerSummary: string | null;
}

/** The status recorded alongside a signed-artifact upload (never `Unsigned` — that's the default absent any upload). */
export const UPLOADABLE_SIGNATURE_STATUSES = ['PartiallySigned', 'Signed'] as const;
export type UploadableSignatureStatus = (typeof UPLOADABLE_SIGNATURE_STATUSES)[number];

export const MAX_SIGNED_ARTIFACT_BYTES = 20 * 1024 * 1024;
