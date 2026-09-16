// Mirrors api/IepAssistant.Api/DTOs/Evaluations/EvaluationDtos.cs (plan7-contract.md,
// Phase 2). Evaluation case lifecycle: referral → consent → clock →
// determination → ETR handoff (plan 7, decision 1).
import type { ObligationDto } from '@/features/obligations/types';

export const EVALUATION_CASE_KINDS = ['Initial', 'Reevaluation'] as const;
export type EvaluationCaseKind = (typeof EVALUATION_CASE_KINDS)[number];
export const EVALUATION_CASE_KIND_LABELS: Record<EvaluationCaseKind, string> = {
  Initial: 'Initial evaluation',
  Reevaluation: 'Reevaluation',
};

export const EVALUATION_CASE_STATUSES = ['Open', 'ConsentPending', 'InProgress', 'Determined', 'Closed'] as const;
export type EvaluationCaseStatus = (typeof EVALUATION_CASE_STATUSES)[number];
export const EVALUATION_CASE_STATUS_LABELS: Record<EvaluationCaseStatus, string> = {
  Open: 'Open',
  ConsentPending: 'Consent pending',
  InProgress: 'In progress',
  Determined: 'Determined',
  Closed: 'Closed',
};

export const ELIGIBILITY_OUTCOMES = ['Eligible', 'NotEligible', 'Withdrawn'] as const;
export type EligibilityOutcome = (typeof ELIGIBILITY_OUTCOMES)[number];
export const ELIGIBILITY_OUTCOME_LABELS: Record<EligibilityOutcome, string> = {
  Eligible: 'Eligible',
  NotEligible: 'Not eligible',
  Withdrawn: 'Withdrawn',
};

export interface EvaluatorAssignmentDto {
  id: number;
  userId: number;
  displayName: string;
  domain: string;
  dueDate: string | null;
  submittedAt: string | null;
  notes: string | null;
  isOverdue: boolean;
}

export interface EvaluationTimelineEntryDto {
  at: string;
  label: string;
}

export interface EvaluationCaseDto {
  id: number;
  studentId: number;
  kind: EvaluationCaseKind;
  status: EvaluationCaseStatus;
  referralDate: string;
  referralSource: string | null;
  consentRequestedAt: string | null;
  consentReceivedAt: string | null;
  hasConsentDocument: boolean;
  consentFileName: string | null;
  determinationDueDate: string | null;
  dueDateOverrideReason: string | null;
  eligibilityOutcome: EligibilityOutcome | null;
  determinationDate: string | null;
  determinationRationale: string | null;
  etrAuthoredVersionId: number | null;
  closedAt: string | null;
  createdByName: string;
  assignments: EvaluatorAssignmentDto[];
  timeline: EvaluationTimelineEntryDto[];
  obligation: ObligationDto | null;
}

export interface CreateEvaluationCaseRequest {
  kind: EvaluationCaseKind;
  referralDate: string;
  referralSource?: string;
}

export interface RequestConsentRequest {
  requestedAt?: string;
}

export interface ReceiveConsentRequest {
  receivedAt: string;
  file?: File;
}

export interface OverrideDueDateRequest {
  determinationDueDate: string;
  reason: string;
}

export interface CreateEvaluatorAssignmentRequest {
  userId: number;
  domain: string;
  dueDate?: string;
}

export interface UpdateEvaluatorAssignmentRequest {
  submittedAt?: string;
  notes?: string;
  dueDate?: string;
}

export interface DetermineEvaluationRequest {
  outcome: EligibilityOutcome;
  determinationDate: string;
  rationale: string;
  etrAuthoredVersionId?: number;
}

export interface CreateIepResponseDto {
  instanceId: number;
}
