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
