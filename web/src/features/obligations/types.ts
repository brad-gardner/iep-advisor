// Mirrors api/IepAssistant.Api/DTOs/Obligations/*.cs (see plan4-contract.md).
// Obligations are a computed read model — never stored, never posted back.

export const OBLIGATION_KINDS = ['AnnualReview', 'Reevaluation', 'EtrDue'] as const;
export type ObligationKind = (typeof OBLIGATION_KINDS)[number];
export const OBLIGATION_KIND_LABELS: Record<ObligationKind, string> = {
  AnnualReview: 'Annual review',
  Reevaluation: 'Reevaluation',
  EtrDue: 'ETR',
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
