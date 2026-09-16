// Staff-side DTOs for plan 6 (plan6-contract.md). Re-exports the shapes shared
// with the parent shared-drafts surface — `@/features/shared-drafts/types` is
// the canonical source — and adds the staff-only ones (mirrors how
// `document-authoring/types.ts` re-exports the admin template DTOs).
export type {
  ChangeFieldDto,
  ChangeRowDto,
  ChangeSummaryDto,
  DraftResponseDto,
  DraftResponseKind,
  DraftResponseStatus,
  RevisionAcknowledgementDto,
  SharedDraftRevisionDto,
  SharedDraftStatus,
} from '@/features/shared-drafts/types';
export { DRAFT_RESPONSE_KIND_LABELS } from '@/features/shared-drafts/types';

import type { ChangeSummaryDto, DraftResponseDto, RevisionAcknowledgementDto, SharedDraftRevisionDto } from '@/features/shared-drafts/types';

export interface ShareRecipientDto {
  userId: number;
  displayName: string;
  relationship: 'Parent' | 'Student';
  email: string;
}

export interface RecipientPreviewDto {
  recipients: ShareRecipientDto[];
  policyEnabled: boolean;
  lastSharedAt: string | null;
  willSupersedeRevision: number | null;
}

export interface ShareDraftRequest {
  message?: string;
}

export interface ResolveResponseRequest {
  staffReply?: string;
  resolvedInDraft?: boolean;
}

export interface ConvergeDto {
  instanceId: number;
  latestRevision: SharedDraftRevisionDto | null;
  openResponses: DraftResponseDto[];
  resolvedResponses: DraftResponseDto[];
  /** Diff of the live draft vs the latest shared revision. */
  changesSinceShare: ChangeSummaryDto | null;
  acknowledgements: RevisionAcknowledgementDto[];
  canShare: boolean;
  policyEnabled: boolean;
}
