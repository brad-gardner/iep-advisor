// Mirrors api/IepAssistant.Api/DTOs/Notifications/*.cs (see plan4-contract.md).

export const NOTIFICATION_KINDS = [
  'MeetingScheduled',
  'MeetingUpdated',
  'MeetingCancelled',
  'MeetingReminder',
  'ObligationDigest',
  'DraftShared',
  'ResponseReceived',
  'Generic',
] as const;
export type NotificationKind = (typeof NOTIFICATION_KINDS)[number];

export interface NotificationDto {
  id: number;
  kind: NotificationKind;
  title: string;
  body: string;
  linkPath: string | null;
  createdAt: string;
  readAt: string | null;
  emailSentAt: string | null;
  emailError: string | null;
}

export interface NotificationListResult {
  items: NotificationDto[];
  unreadCount: number;
}

export interface MarkAllReadResult {
  marked: number;
}
