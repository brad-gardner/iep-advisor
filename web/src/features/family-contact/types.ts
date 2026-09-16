// Mirrors api/IepAssistant.Api/DTOs/FamilyContact/FamilyContactDtos.cs (plan7-contract.md
// "Phase 3", decision 7). Offline family participation: attempts to reach the
// family, and input the family gave outside the app.

export const FAMILY_CONTACT_METHODS = ['Email', 'Phone', 'Letter', 'InPerson', 'Portal'] as const;
export type FamilyContactMethod = (typeof FAMILY_CONTACT_METHODS)[number];
export const FAMILY_CONTACT_METHOD_LABELS: Record<FamilyContactMethod, string> = {
  Email: 'Email',
  Phone: 'Phone',
  Letter: 'Letter',
  InPerson: 'In person',
  Portal: 'Portal',
};

export const FAMILY_CONTACT_OUTCOMES = ['Reached', 'NoAnswer', 'LeftMessage', 'Declined', 'Returned'] as const;
export type FamilyContactOutcome = (typeof FAMILY_CONTACT_OUTCOMES)[number];
export const FAMILY_CONTACT_OUTCOME_LABELS: Record<FamilyContactOutcome, string> = {
  Reached: 'Reached',
  NoAnswer: 'No answer',
  LeftMessage: 'Left a message',
  Declined: 'Declined',
  Returned: 'Call/message returned',
};

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
