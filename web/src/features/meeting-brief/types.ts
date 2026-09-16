// Mirrors api/IepAssistant.Api/DTOs/Meetings/MeetingBriefDtos.cs (plan7-contract.md
// "Phase 3", decision 2). A server-composed, cached, regenerable pre-meeting
// brief for the LEA rep. Advisory language only.
import type { ChangeSummaryDto, DraftResponseDto } from '@/features/shared-drafts/types';
import type { FamilyContactAttemptDto, OfflineFamilyInputDto } from '@/features/family-contact/types';

export const BRIEF_SOURCE_KINDS = ['SharedRevision', 'Draft'] as const;
export type BriefSourceKind = (typeof BRIEF_SOURCE_KINDS)[number];
export const BRIEF_SOURCE_LABELS: Record<BriefSourceKind, string> = {
  SharedRevision: 'Shared revision',
  Draft: 'Live draft',
};

export const RESOURCE_COMMITMENT_KINDS = [
  'NewService',
  'ChangedService',
  'Placement',
  'Esy',
  'OneToOne',
  'Transportation',
] as const;
export type ResourceCommitmentKind = (typeof RESOURCE_COMMITMENT_KINDS)[number];
export const RESOURCE_COMMITMENT_KIND_LABELS: Record<ResourceCommitmentKind, string> = {
  NewService: 'New service',
  ChangedService: 'Changed service',
  Placement: 'Placement',
  Esy: 'Extended school year',
  OneToOne: '1:1 support',
  Transportation: 'Transportation',
};

export interface BriefSourceDto {
  kind: BriefSourceKind;
  id: number;
  label: string;
}

export interface ResourceCommitmentDto {
  fieldKey: string;
  rowId: string | null;
  label: string;
  kind: ResourceCommitmentKind;
  detail: string;
}

export interface BriefChecklistItemDto {
  key: string;
  label: string;
  /** `null` means "unknown" (rendered as —), not failing. */
  satisfied: boolean | null;
  detail: string | null;
}

export interface MeetingBriefDto {
  meetingId: number;
  generatedAt: string;
  source: BriefSourceDto | null;
  summary: string;
  changes: ChangeSummaryDto | null;
  resourceCommitments: ResourceCommitmentDto[];
  checklist: BriefChecklistItemDto[];
  openFamilyResponses: DraftResponseDto[];
  offlineInput: OfflineFamilyInputDto[];
  contactAttempts: FamilyContactAttemptDto[];
  disclaimer: string;
}
