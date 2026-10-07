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
 * `kind` is typed as `DraftResponseKind` so a typo at the call site is still
 * a `tsc` error, but the value itself comes from stored/server data that can
 * outlive the client's known set, so a lookup miss falls back to the raw
 * value via `defaultValue` rather than showing a raw
 * `shared-drafts:responseKind.*` key.
 */
export function draftResponseKindLabel(kind: DraftResponseKind): string {
  return i18n.t(`shared-drafts:responseKind.${kind}`, { defaultValue: kind });
}
