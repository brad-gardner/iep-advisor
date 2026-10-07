import i18n from './i18n';
import type { ObligationKind, ObligationStatus } from '@/features/obligations/types';

// Translated obligation enum labels (`obligations:kind.*`,
// `obligations:status.*`). Same shape as `inviteStatusLabel`/
// `meetingTypeLabel`: plain functions over `i18n.t`, callable from render
// bodies; callers re-render on language change through their own
// `useTranslation`. `obligations` is a staff-only namespace (plan phase 5 —
// see `docs/i18n/README.md`'s "Staff and admin namespaces"); every current
// caller is reached only from a staff page, so its English is always
// registered (`app/lazy-routes/staff-locales.ts`) before these are called.
//
// `defaultValue` falls back to the raw enum value, same as
// `meetingTypeLabel`/`meetingDecisionOutcomeLabel`/`familyContactMethodLabel`
// — a defensive fallback for a context that renders before this namespace
// registers (e.g. a unit test that doesn't import `staff-locales`), not a
// hand-maintained English copy to keep in sync (the former
// `OBLIGATION_KIND_LABELS`/`STATUS_ENGLISH_FALLBACK` maps this replaced).
export function obligationKindLabel(kind: ObligationKind): string {
  return i18n.t(`obligations:kind.${kind}`, { defaultValue: kind });
}

export function obligationStatusLabel(status: ObligationStatus): string {
  return i18n.t(`obligations:status.${status}`, { defaultValue: status });
}
