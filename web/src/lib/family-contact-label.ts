import i18n from './i18n';
import type { FamilyContactMethod, FamilyContactOutcome } from '@/features/family-contact/types';

// Translated family-contact enum labels (`family-contact:method.*`,
// `family-contact:outcome.*`). Same shape as `inviteStatusLabel`/
// `meetingTypeLabel`: plain functions over `i18n.t`, callable from render
// bodies; callers re-render on language change through their own
// `useTranslation`. `family-contact` is a staff-only namespace (plan phase
// 5 — see `docs/i18n/README.md`'s "Staff and admin namespaces"); every
// caller is reached only from a staff page (the educator student detail
// page, or `features/meeting-brief/pages/meeting-brief-page.tsx`, which
// names `family-contact` in its own `useTranslation` call) — the old
// `FAMILY_CONTACT_METHOD_LABELS`/`FAMILY_CONTACT_OUTCOME_LABELS` English
// maps (`@/features/family-contact/types`) were removed outright once that
// page moved to this helper. A lookup miss falls back to the raw value via
// `defaultValue`.
export function familyContactMethodLabel(method: FamilyContactMethod): string {
  return i18n.t(`family-contact:method.${method}`, { defaultValue: method });
}

export function familyContactOutcomeLabel(outcome: FamilyContactOutcome): string {
  return i18n.t(`family-contact:outcome.${outcome}`, { defaultValue: outcome });
}
