// Mirrors api/IepAssistant.Api/DTOs/FamilyContact/FamilyContactDtos.cs (plan7-contract.md
// "Phase 3", decision 7). Offline family participation: attempts to reach the
// family, and input the family gave outside the app.

export const FAMILY_CONTACT_METHODS = ['Email', 'Phone', 'Letter', 'InPerson', 'Portal'] as const;
export type FamilyContactMethod = (typeof FAMILY_CONTACT_METHODS)[number];

export const FAMILY_CONTACT_OUTCOMES = ['Reached', 'NoAnswer', 'LeftMessage', 'Declined', 'Returned'] as const;
export type FamilyContactOutcome = (typeof FAMILY_CONTACT_OUTCOMES)[number];
// Display labels: `@/lib/family-contact-label.ts`'s `familyContactMethodLabel`/
// `familyContactOutcomeLabel`, translated via the staff-only `family-contact`
// namespace (i18n plan phase 5). Every caller now uses those helpers, so the
// old `FAMILY_CONTACT_METHOD_LABELS`/`FAMILY_CONTACT_OUTCOME_LABELS` English
// maps were removed outright.

export interface FamilyContactAttemptDto {
  id: number;
  schoolStudentId: number;
  attemptedAt: string;
  method: FamilyContactMethod;
  outcome: FamilyContactOutcome;
  note: string | null;
  recordedByUserId: number;
  recordedByName: string | null;
}

export interface CreateFamilyContactAttemptRequest {
  attemptedAt?: string;
  method: FamilyContactMethod;
  outcome: FamilyContactOutcome;
  note?: string;
}

export interface OfflineFamilyInputDto {
  id: number;
  schoolStudentId: number;
  documentInstanceId: number | null;
  receivedAt: string;
  method: FamilyContactMethod;
  summary: string;
  recordedByUserId: number;
  recordedByName: string | null;
}

export interface CreateOfflineFamilyInputRequest {
  documentInstanceId?: number;
  receivedAt?: string;
  method: FamilyContactMethod;
  summary: string;
}
