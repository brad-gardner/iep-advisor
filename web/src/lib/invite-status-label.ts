import i18n from './i18n';
import type { InviteStatus } from '@/features/meetings/types';

// Human-facing, translated label for a meeting invite's RSVP status (the
// Pending/Accepted/Declined/Tentative badge shown on "next meeting"/
// "upcoming meeting" cards) — via `common:inviteStatus.status.*`. Follows
// `orgRoleLabel`'s shape: a plain function calling `i18n.t` directly (not
// `useTranslation`), since it's called from render bodies and plain code
// alike. Every `InviteStatus` has a translation (unlike `orgRoleLabel`'s
// DB-sourced role names), so there's no fallback to pass — an unrecognized
// value is a `tsc` error at the call site, not a runtime concern.
export function inviteStatusLabel(status: InviteStatus): string {
  return i18n.t(`common:inviteStatus.status.${status}`);
}
