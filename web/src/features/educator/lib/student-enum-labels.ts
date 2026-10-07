import i18n from '@/lib/i18n';
import type { AccessRole, AttentionFilter, ExitReason, StudentStatus, TeamRole } from '../types';

// Human-facing, translated labels for the staff-only enum maps in
// `features/educator/types.ts` (`STUDENT_STATUS_LABELS`, `EXIT_REASON_LABELS`,
// `TEAM_ROLE_LABELS`, `ATTENTION_FILTER_LABELS`, and the `AccessRole` string
// union) — via `educator:studentStatus.*` / `exitReason.*` / `teamRole.*` /
// `attentionFilter.*` / `accessRole.*` (all keyed by the raw enum value,
// which IS the stored/API value for every one of these — see
// `docs/i18n/README.md`'s "Display-label helpers follow `orgRoleLabel`'s
// shape"). Plain functions calling `i18n.t` directly (not `useTranslation`)
// so they work from both render bodies and plain builder functions
// (`roster-columns.tsx`, `team-columns.tsx`) that have no hook of their own;
// the `educator` namespace's English is guaranteed already registered by
// the time any of these run, since every caller lives behind the staff
// lazy-route chunk that registers it (`features/educator/staff-locales.ts`).
// Every value in each union has a translation, so there's no legacy/unknown
// fallback to pass — an unrecognized value is a `tsc` error at the call
// site, same as `inviteStatusLabel`.

export function studentStatusLabel(status: StudentStatus): string {
  return i18n.t(`educator:studentStatus.${status}`);
}

export function exitReasonLabel(reason: ExitReason): string {
  return i18n.t(`educator:exitReason.${reason}`);
}

export function teamRoleLabel(role: TeamRole): string {
  return i18n.t(`educator:teamRole.${role}`);
}

export function accessRoleLabel(role: AccessRole): string {
  return i18n.t(`educator:accessRole.${role}`);
}

/**
 * `AttentionFilter`'s label for the roster's "needs attention" banner.
 * `DueInRange` normally carries its own caller-chosen `from`/`to` window
 * (built by the page from `educator:studentsPage.dueBetween`, not this map)
 * — this entry only covers a `DueInRange` deep link with no date bounds
 * attached.
 */
export function attentionFilterLabel(filter: AttentionFilter): string {
  return i18n.t(`educator:attentionFilter.${filter}`);
}
