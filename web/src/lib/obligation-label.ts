import i18n from './i18n';
import { OBLIGATION_KIND_LABELS, type ObligationKind, type ObligationStatus } from '@/features/obligations/types';

// Translated obligation enum labels (`obligations:kind.*`,
// `obligations:status.*`). Same shape as `inviteStatusLabel`/
// `meetingTypeLabel`: plain functions over `i18n.t`, callable from render
// bodies; callers re-render on language change through their own
// `useTranslation`. `obligations` is a staff-only namespace (plan phase 5 —
// see `docs/i18n/README.md`'s "Staff and admin namespaces"); every current
// caller within `features/obligations`/`features/evaluation` is reached
// only from the educator student detail page, so its English is always
// registered (`features/obligations/staff-locales.ts`) before these are
// called from there.
//
// `features/home/components/due-soon-section.tsx`,
// `overdue-by-case-manager-table.tsx` and
// `features/calendar/components/calendar-agenda-list.tsx` still render
// `ObligationStatusChip` (and import the English-only `OBLIGATION_KIND_LABELS`
// map directly) — those features convert in a later phase. Unlike
// `meetingTypeLabel`'s `defaultValue` (a defensive fallback for a value the
// client doesn't know about yet), the `defaultValue` below is load-bearing
// TODAY: if one of those not-yet-converted callers renders
// `ObligationStatusChip` in a context that never registered this
// namespace (e.g. an out-of-scope unit test that doesn't import
// `staff-locales`), `i18n.t` falls back to it synchronously — so it must be
// the correct ENGLISH DISPLAY text (matching `OBLIGATION_KIND_LABELS`
// exactly), never the raw enum value, or those callers would silently
// regress to showing "DueSoon" instead of "Due soon".
export function obligationKindLabel(kind: ObligationKind): string {
  return i18n.t(`obligations:kind.${kind}`, { defaultValue: OBLIGATION_KIND_LABELS[kind] });
}

const STATUS_ENGLISH_FALLBACK: Record<ObligationStatus, string> = {
  Upcoming: 'Upcoming',
  DueSoon: 'Due soon',
  Overdue: 'Overdue',
  Unknown: 'Unknown',
};

export function obligationStatusLabel(status: ObligationStatus): string {
  return i18n.t(`obligations:status.${status}`, { defaultValue: STATUS_ENGLISH_FALLBACK[status] });
}
