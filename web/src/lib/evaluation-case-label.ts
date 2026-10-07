import i18n from './i18n';
import type { EligibilityOutcome, EvaluationCaseKind, EvaluationCaseStatus } from '@/features/evaluation/types';

// Translated evaluation-case enum labels (`evaluation:caseKind.*`,
// `evaluation:caseStatus.*`, `evaluation:eligibilityOutcome.*`). Same shape
// as `inviteStatusLabel`/`meetingTypeLabel`: plain functions over `i18n.t`,
// callable from render bodies; callers re-render on language change through
// their own `useTranslation`. `evaluation` is a staff-only namespace (plan
// phase 5 — see `docs/i18n/README.md`'s "Staff and admin namespaces"); every
// current and foreseeable caller is a staff-only component reached from the
// educator student detail page, so its English is always registered
// (`features/evaluation/staff-locales.ts`) before any of these can be
// called. A lookup miss falls back to the raw value via `defaultValue`,
// matching `meetingTypeLabel`'s reasoning: these are typed enums, but the
// value ultimately comes from stored/server data.
export function evaluationCaseKindLabel(kind: EvaluationCaseKind): string {
  return i18n.t(`evaluation:caseKind.${kind}`, { defaultValue: kind });
}

export function evaluationCaseStatusLabel(status: EvaluationCaseStatus): string {
  return i18n.t(`evaluation:caseStatus.${status}`, { defaultValue: status });
}

export function eligibilityOutcomeLabel(outcome: EligibilityOutcome): string {
  return i18n.t(`evaluation:eligibilityOutcome.${outcome}`, { defaultValue: outcome });
}
