import i18n from './i18n';
import type { ParentContributionKind } from '@/features/contributions/api/contributions-api';

// Translated label for a parent's "about my child at home" note kind
// (`contributions:kind.*`). Same shape as `inviteStatusLabel`/
// `meetingTypeLabel`: a plain function over `i18n.t`, callable from render
// bodies; callers re-render on language change through their own
// `useTranslation`. Unlike the staff-only helpers in this file's siblings,
// `contributions` is a normal EAGER namespace — `AboutMyChildCard` renders
// on the parent child-detail page (`features/children/components/
// child-overview-tab.tsx`), so its English is always already bundled (see
// `docs/i18n/README.md`'s "Resources"); no `staff-locales` registration is
// needed.
export function contributionKindLabel(kind: ParentContributionKind): string {
  return i18n.t(`contributions:kind.${kind}`, { defaultValue: kind });
}
