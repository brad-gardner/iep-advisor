// Mirrors api/IepAssistant.Api/DTOs/SharedDrafts/*.cs (plan6-contract.md). Canonical
// home for the DTOs shared between the parent shared-drafts surface and the
// staff draft-sharing surface — `features/draft-sharing/types.ts` re-exports the
// shared shapes rather than redefining them (same idiom as
// `document-authoring/types.ts` re-exporting the admin template DTOs).
import type { TemplateVersionDetailDto } from '@/features/admin/templates/types';

export const SHARED_DRAFT_STATUSES = ['Active', 'Superseded', 'Withdrawn'] as const;
export type SharedDraftStatus = (typeof SHARED_DRAFT_STATUSES)[number];

export const DRAFT_RESPONSE_KINDS = ['Agree', 'Question', 'ChangeRequest', 'Comment'] as const;
export type DraftResponseKind = (typeof DRAFT_RESPONSE_KINDS)[number];
export const DRAFT_RESPONSE_KIND_LABELS: Record<DraftResponseKind, string> = {
  Agree: 'Agree',
  Question: 'Question',
  ChangeRequest: 'Request a change',
  Comment: 'Comment',
};

export const DRAFT_RESPONSE_STATUSES = ['Open', 'Resolved'] as const;
export type DraftResponseStatus = (typeof DRAFT_RESPONSE_STATUSES)[number];

export interface ChangeRowDto {
  fieldKey: string;
  fieldLabel: string;
  rowId: string;
  /** Primary column text for the row, e.g. the goal text. */
  label: string;
}

export interface ChangeFieldDto {
  fieldKey: string;
  fieldLabel: string;
}

export interface ChangeSummaryDto {
  addedRows: ChangeRowDto[];
  removedRows: ChangeRowDto[];
  changedRows: ChangeRowDto[];
  changedFields: ChangeFieldDto[];
  summaryText: string;
}

export interface RevisionAcknowledgementDto {
  parentName: string;
  acknowledgedAt: string;
}

export interface SharedDraftRevisionDto {
  id: number;
  documentInstanceId: number;
  studentId: number;
  studentName: string;
  documentTypeKey: string;
  documentTypeDisplayName: string;
  revisionNumber: number;
  status: SharedDraftStatus;
  sharedAt: string;
  sharedByName: string;
  message: string | null;
  withdrawnAt: string | null;
  changeSummary: ChangeSummaryDto | null;
  /** This parent's own acknowledgement stamp; null on the staff view. */
  acknowledgedAt: string | null;
  /** Populated on the staff view only. */
  acknowledgements?: RevisionAcknowledgementDto[];
  openResponseCount: number;
  templateVersionId: number;
}

export interface SharedDraftRevisionDetailDto extends SharedDraftRevisionDto {
  /** Frozen value-document keyed by `FieldKey` (guid). */
  values: Record<string, unknown>;
  templateVersion: TemplateVersionDetailDto;
}

export interface DraftExplanationSectionDto {
  /** The template section's id (as a string) when the server could resolve the
   *  model's title to one; otherwise an ordinal (`s1`, `s2`, …) and `title` is
   *  the only handle. */
  sectionId: string;
  title: string;
  explanation: string;
}

export interface DraftExplanationItemDto {
  fieldKey: string;
  rowId: string | null;
  label: string;
  explanation: string;
}

export interface DraftExplanationDto {
  revisionId: number;
  generatedAt: string;
  sections: DraftExplanationSectionDto[];
  items: DraftExplanationItemDto[];
  disclaimer: string;
}

export interface AskQuestionRequest {
  question: string;
  targetFieldKey?: string;
  targetRowId?: string;
}

export interface DraftAnswerCitationDto {
  /** Null when the citation is not tied to a template field. */
  fieldKey: string | null;
  rowId: string | null;
  label: string;
  excerpt: string;
}

export interface DraftAnswerDto {
  noteId: number;
  question: string;
  answer: string;
  citations: DraftAnswerCitationDto[];
  answeredAt: string;
  disclaimer: string;
}

/** Private to the parent who asked — never exposed to staff. */
export interface ParentDraftNoteDto {
  id: number;
  revisionId: number;
  question: string;
  answer: string;
  targetFieldKey: string | null;
  targetRowId: string | null;
  /** What in the revision the answer was grounded in (persisted with the note). */
  citations: DraftAnswerCitationDto[];
  createdAt: string;
}

export interface DraftResponseDto {
  id: number;
  revisionId: number;
  parentUserId: number;
  parentName: string;
  targetFieldKey: string | null;
  targetRowId: string | null;
  targetLabel: string | null;
  kind: DraftResponseKind;
  text: string;
  createdAt: string;
  status: DraftResponseStatus;
  staffReply: string | null;
  resolvedInDraft: boolean;
  resolvedByName: string | null;
  resolvedAt: string | null;
}

export interface CreateResponseRequest {
  kind: DraftResponseKind;
  text: string;
  targetFieldKey?: string;
  targetRowId?: string;
}

export interface MeetingSummaryRecipientDto {
  displayName: string;
  email: string;
}

export type MeetingSummaryStatus = 'Draft' | 'Sent';

export interface MeetingSummaryDto {
  id: number;
  meetingId: number;
  status: MeetingSummaryStatus;
  body: string;
  generatedAt: string | null;
  editedAt: string | null;
  sentAt: string | null;
  sentByName: string | null;
  recipients: MeetingSummaryRecipientDto[];
}
