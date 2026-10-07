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

// Human-facing labels for these vocabularies used to live here as plain
// English `Record`s (`FIELD_SEMANTIC_LABELS`/`COLUMN_SEMANTIC_LABELS`), but
// this file sits on an EAGERLY reachable import path (`features/shared-drafts/
// lib/semantic-rows.ts` imports its types, and that feature is bundled
// eagerly) — `lib/i18n/staff-namespace-boundary.test.ts` would flag any
// `t('admin:...')` call placed directly in this file as a staff-only
// namespace reached from eager code. The translated replacements
// (`fieldSemanticLabel`/`columnSemanticLabel`) live in
// `./lib/semantic-labels.ts` instead — a file only `features/admin/templates`'
// own (lazy, platform-admin-only) components import — same reasoning as
// `features/document-authoring/lib/row-block-item-label.ts`'s own doc
// comment. See `docs/i18n/README.md`'s "Staff and admin namespaces".

/** Semantics that mark a repeating structured block whose rows carry identity. */
export const ROW_BLOCK_SEMANTICS: ReadonlySet<FieldSemantic> = new Set<FieldSemantic>([
  'goals',
  'services',
  'accommodations',
  'transition',
  'participants',
  'evaluatorReports',
]);

/**
 * Semantics whose rows may carry a `_ownerUserId` (plan 2026-10-02-002, "read-first
 * sections, goals/services, item owners") — mirrors api/IepAssistant.Services/Models/
 * DocumentSemantics.cs `FieldSemantics.OwnerEligible`. Deliberately NOT participants/
 * evaluatorReports: those rows already name a person.
 */
export const OWNER_ELIGIBLE_SEMANTICS: ReadonlySet<FieldSemantic> = new Set<FieldSemantic>([
  'goals',
  'services',
  'accommodations',
  'transition',
]);

// A translated singular row-item label ("Goal", "Service", …) used to live
// here too (`ROW_BLOCK_ITEM_LABELS`/`rowBlockItemLabel`), but nothing calls
// it any more — `features/document-authoring` (the only consumer of this
// concept) has its own translated `rowBlockItemNoun` in
// `lib/row-block-item-label.ts`, covering the same semantics. Removed rather
// than kept as dead English-only code.

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
/**
 * Reserved row-object key (plan 2026-10-02-002): the user id of the student-team
 * member responsible for this row. Kept only on rows of an `OWNER_ELIGIBLE_SEMANTICS`
 * table, and only while that user is an active team member — the server re-validates
 * on every save and drops a stale/invalid value (with a field-level save warning).
 * Mirrors `RowMetaKeys.OwnerUserId`.
 */
export const ROW_OWNER_USER_ID_KEY = '_ownerUserId';
/**
 * Output-only, role-display substitute for `ROW_OWNER_USER_ID_KEY` on family/student-
 * facing value documents — never the person's name. The web client never reads or
 * sends this key; it exists here only to document the reserved vocabulary. Mirrors
 * `RowMetaKeys.OwnerRole`.
 */
export const ROW_OWNER_ROLE_KEY = '_ownerRole';
/**
 * Goal rows only (Phase 3): an ordered array of objective/benchmark objects, each
 * `{ _rowId, description, criteria, targetDate }`. Mirrors `RowMetaKeys.Objectives`.
 */
export const ROW_OBJECTIVES_KEY = '_objectives';
