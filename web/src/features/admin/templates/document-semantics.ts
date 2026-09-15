// Closed vocabularies for the optional `semantic` tag on template fields and
// Table columns — mirrors the backend FieldSemantics / ColumnSemantics allow-lists
// (api/IepAssistant.Services/Models/DocumentSemantics.cs). Semantics let AI
// assist, prefill, PDF layout and projections find "the goals table" or "the
// baseline column" in any template without knowing FieldKey GUIDs.

export const FIELD_SEMANTICS = [
  'studentProfile',
  'presentLevels',
  'eligibility',
  'placement',
  'progressMonitoring',
  'specialFactors',
  'goals',
  'services',
  'accommodations',
  'transition',
  'futurePlanning',
  'extendedSchoolYear',
  'testing',
  'transportation',
  'lre',
  'participants',
  'signatures',
  'referralReason',
  'evaluationPlan',
  'evaluatorReports',
  'teamSummary',
  'eligibilityDetermination',
  'meetingDate',
  'effectiveDates',
] as const;
export type FieldSemantic = (typeof FIELD_SEMANTICS)[number];

export const FIELD_SEMANTIC_LABELS: Record<FieldSemantic, string> = {
  studentProfile: 'Student profile',
  presentLevels: 'Present levels (PLAAFP)',
  eligibility: 'Eligibility / disability category',
  placement: 'Placement',
  progressMonitoring: 'Progress monitoring / reporting',
  specialFactors: 'Special factors',
  goals: 'Goals (repeating block)',
  services: 'Services (repeating block)',
  accommodations: 'Accommodations (repeating block)',
  transition: 'Transition (repeating block)',
  futurePlanning: 'Future planning',
  extendedSchoolYear: 'Extended school year',
  testing: 'Statewide / district testing',
  transportation: 'Transportation',
  lre: 'Least restrictive environment',
  participants: 'Meeting participants (repeating block)',
  signatures: 'Signatures',
  referralReason: 'Referral reason',
  evaluationPlan: 'Evaluation plan',
  evaluatorReports: 'Evaluator reports (repeating block)',
  teamSummary: 'Team summary',
  eligibilityDetermination: 'Eligibility determination',
  meetingDate: 'Meeting date',
  effectiveDates: 'Effective dates',
};

/** Semantics that mark a repeating structured block whose rows carry identity. */
export const ROW_BLOCK_SEMANTICS: ReadonlySet<FieldSemantic> = new Set<FieldSemantic>([
  'goals',
  'services',
  'accommodations',
  'transition',
  'participants',
  'evaluatorReports',
]);

export const COLUMN_SEMANTICS = [
  'domain',
  'goalText',
  'baseline',
  'targetCriteria',
  'measurementMethod',
  'timeframe',
  'serviceType',
  'frequency',
  'duration',
  'location',
  'providerRole',
  'startDate',
  'endDate',
  'category',
  'accommodation',
  'goalArea',
  'transitionServices',
  'participantName',
  'participantRole',
  'attended',
  'evaluationDomain',
  'evaluatorName',
  'findings',
] as const;
export type ColumnSemantic = (typeof COLUMN_SEMANTICS)[number];

export const COLUMN_SEMANTIC_LABELS: Record<ColumnSemantic, string> = {
  domain: 'Goal domain / area',
  goalText: 'Goal text',
  baseline: 'Baseline',
  targetCriteria: 'Target criteria',
  measurementMethod: 'Measurement method',
  timeframe: 'Timeframe',
  serviceType: 'Service type',
  frequency: 'Frequency',
  duration: 'Duration',
  location: 'Location',
  providerRole: 'Provider role',
  startDate: 'Start date',
  endDate: 'End date',
  category: 'Accommodation category',
  accommodation: 'Accommodation',
  goalArea: 'Postsecondary goal area',
  transitionServices: 'Transition services',
  participantName: 'Participant name',
  participantRole: 'Participant role',
  attended: 'Attended',
  evaluationDomain: 'Evaluation area',
  evaluatorName: 'Evaluator',
  findings: 'Findings',
};

export function isFieldSemantic(v: unknown): v is FieldSemantic {
  return typeof v === 'string' && (FIELD_SEMANTICS as readonly string[]).includes(v);
}
export function isColumnSemantic(v: unknown): v is ColumnSemantic {
  return typeof v === 'string' && (COLUMN_SEMANTICS as readonly string[]).includes(v);
}

/** Reserved row-object key carrying the server-assigned stable row identity. */
export const ROW_ID_KEY = '_rowId';
/** Reserved row-object key: provenance of a row prefilled from a prior finalized version. */
export const ROW_CARRIED_FROM_KEY = '_carriedFrom';
/** Reserved row-object key: true once a carried row has been kept or edited. */
export const ROW_CONFIRMED_KEY = '_confirmed';
