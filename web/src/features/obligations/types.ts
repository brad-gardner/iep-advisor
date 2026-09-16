// Mirrors api/IepAssistant.Api/DTOs/Obligations/*.cs (see plan4-contract.md).
// Obligations are a computed read model — never stored, never posted back.
import { Stethoscope, Target, UserCheck, type LucideIcon } from 'lucide-react';

export const OBLIGATION_KINDS = [
  'AnnualReview',
  'Reevaluation',
  'EtrDue',
  // Plan 7: an Active goal with no observation in 45+ days.
  'GoalObservationStale',
  // Plan 7: an open evaluation case's determination clock.
  'EvaluationDetermination',
  // Plan 7: an overdue (not yet submitted) evaluator assignment.
  'EvaluatorSubmission',
] as const;
export type ObligationKind = (typeof OBLIGATION_KINDS)[number];
export const OBLIGATION_KIND_LABELS: Record<ObligationKind, string> = {
  AnnualReview: 'Annual review',
  Reevaluation: 'Reevaluation',
  EtrDue: 'ETR',
  GoalObservationStale: 'Goal progress overdue',
  EvaluationDetermination: 'Evaluation determination',
  EvaluatorSubmission: 'Evaluator submission',
};

/** Icons for the plan 7 obligation kinds only (never colour/icon-only — every
 *  row still carries its text label alongside). The plan 4 kinds render
 *  without an icon, unchanged. */
export const OBLIGATION_KIND_ICONS: Partial<Record<ObligationKind, LucideIcon>> = {
  GoalObservationStale: Target,
  EvaluationDetermination: Stethoscope,
  EvaluatorSubmission: UserCheck,
};

export const OBLIGATION_STATUSES = ['Upcoming', 'DueSoon', 'Overdue', 'Unknown'] as const;
export type ObligationStatus = (typeof OBLIGATION_STATUSES)[number];
export const OBLIGATION_STATUS_LABELS: Record<ObligationStatus, string> = {
  Upcoming: 'Upcoming',
  DueSoon: 'Due soon',
  Overdue: 'Overdue',
  Unknown: 'Unknown',
};

export interface ObligationDto {
  kind: ObligationKind;
  dueDate: string | null;
  status: ObligationStatus;
  sourceLabel: string;
  ownerUserId: number | null;
  ownerName: string | null;
  schoolStudentId: number;
  studentName: string;
  daysUntilDue: number | null;
  ruleProfile: 'OH' | 'Default';
}
