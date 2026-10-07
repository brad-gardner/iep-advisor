import i18n from './i18n';
import type { FamilyContactMethod, FamilyContactOutcome } from '@/features/family-contact/types';

// Translated family-contact enum labels (`family-contact:method.*`,
// `family-contact:outcome.*`). Same shape as `inviteStatusLabel`/
// `meetingTypeLabel`: plain functions over `i18n.t`, callable from render
// bodies; callers re-render on language change through their own
// `useTranslation`. `family-contact` is a staff-only namespace (plan phase
// 5 — see `docs/i18n/README.md`'s "Staff and admin namespaces"); every
// in-scope caller is reached only from the educator student detail page.
//
// `features/meeting-brief/pages/meeting-brief-page.tsx` still imports the
// English-only `FAMILY_CONTACT_METHOD_LABELS`/`FAMILY_CONTACT_OUTCOME_LABELS`
// maps directly (`@/features/family-contact/types`) — that feature converts
// in a later phase, so those maps stay in place (not removed) until that
// caller has moved to this helper. A lookup miss falls back to the raw
// value via `defaultValue`.
export function familyContactMethodLabel(method: FamilyContactMethod): string {
  return i18n.t(`family-contact:method.${method}`, { defaultValue: method });
}

export function familyContactOutcomeLabel(outcome: FamilyContactOutcome): string {
  return i18n.t(`family-contact:outcome.${outcome}`, { defaultValue: outcome });
}
