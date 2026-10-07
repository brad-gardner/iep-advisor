import i18n from './i18n';
import type { MeetingStatus, MeetingType } from '@/features/meetings/types';

// Translated meeting type / status labels (`common:meetingType.*`,
// `common:meetingStatus.*`). Same shape as `inviteStatusLabel`: plain functions
// over `i18n.t`, callable from render bodies; callers re-render on language
// change through their own `useTranslation`.
export function meetingTypeLabel(type: MeetingType): string {
  return i18n.t(`common:meetingType.${type}`);
}

export function meetingStatusLabel(status: MeetingStatus): string {
  return i18n.t(`common:meetingStatus.${status}`);
}

// `IepDocument.meetingType` (and the identical fields on a comparison
// timeline entry) is a separate, lowercase_underscore free-text value
// (`"initial"`, `"annual_review"`, `"amendment"`, `"reevaluation"`) — NOT
// the `Meeting` entity's own `MeetingType` enum (PascalCase, a superset:
// also `EtrEligibility`, `Transition`, `ManifestationDetermination`,
// `Other`). Collapses 3 duplicated local `MEETING_TYPE_LABELS` maps
// (iep-documents' iep-document-list.tsx and iep-viewer-page.tsx,
// iep-comparison's iep-timeline.tsx) by normalizing to the matching
// `MeetingType` key and reusing its translation (`common:meetingType.*`)
// rather than adding new `common` keys. An unrecognized value falls back
// to itself, same as every prior call site.
const DOCUMENT_MEETING_TYPE_TO_MEETING_TYPE: Record<string, MeetingType> = {
  initial: 'InitialIep',
  annual_review: 'AnnualReview',
  amendment: 'Amendment',
  reevaluation: 'Reevaluation',
};

export function documentMeetingTypeLabel(value: string): string {
  const meetingType = DOCUMENT_MEETING_TYPE_TO_MEETING_TYPE[value];
  return meetingType ? meetingTypeLabel(meetingType) : value;
}
