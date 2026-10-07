import i18n from '@/lib/i18n';
import type { DraftResponseKind } from '../types';

/**
 * Translated label for a parent's response kind (Agree/Question/
 * ChangeRequest/Comment) on a shared-draft item — via
 * `shared-drafts:responseKind.<Kind>`. Same shape as `meetingTypeLabel`/
 * `meetingStatusLabel` (`lib/meeting-labels.ts`): a plain function over
 * `i18n.t`, callable from render bodies and plain code alike. Canonical home
 * here alongside `DraftResponseKind` itself; `features/draft-sharing` (the
 * staff mirror of this feature) imports it directly rather than duplicating
 * it, same as it already does for `ChangeSummaryChips` and the shared DTOs.
 * Every `DraftResponseKind` has a translation, so an unrecognized value is a
 * `tsc` error at the call site, not a runtime concern.
 */
export function draftResponseKindLabel(kind: DraftResponseKind): string {
  return i18n.t(`shared-drafts:responseKind.${kind}`);
}
