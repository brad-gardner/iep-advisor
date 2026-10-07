import i18n from '@/lib/i18n';

/**
 * Translated label for an `EtrDocument.evaluationType` value
 * (`"initial"`/`"reevaluation"`/`"transfer"`/`"other"`) — for DISPLAY ONLY.
 * Replaces the plain `EVALUATION_TYPE_LABELS` map that used to live on
 * `../types`. An unrecognized value falls back to itself, same as every
 * prior call site.
 */
export function evaluationTypeLabel(value: string | null | undefined): string {
  if (!value) return '';
  return i18n.t(`etr-documents:evaluationType.${value}`, { defaultValue: value });
}

/**
 * Translated label for an `EtrDocument.documentState` value
 * (`"draft"`/`"final"`) — for DISPLAY ONLY. Replaces the plain
 * `DOCUMENT_STATE_LABELS` map that used to live on `../types`.
 */
export function documentStateLabel(value: string | null | undefined): string {
  if (!value) return '';
  return i18n.t(`etr-documents:documentState.${value}`, { defaultValue: value });
}
