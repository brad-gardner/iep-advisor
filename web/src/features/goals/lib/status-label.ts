import i18n from '@/lib/i18n';
import type { GoalRecordStatus } from '../types';

/**
 * Translated label for a goal record's status (`goals:status.*`), following
 * `orgRoleLabel`'s shape: a plain function over `i18n.t`, callable from
 * render bodies and plain code alike. `status` is typed as `GoalRecordStatus`
 * so a typo at the call site is still a `tsc` error, but the value itself
 * comes from stored/server data that can outlive the client's known set, so
 * a lookup miss falls back to the raw value via `defaultValue` rather than
 * showing a raw `goals:status.*` key.
 */
export function goalStatusLabel(status: GoalRecordStatus): string {
  return i18n.t(`goals:status.${status}`, { defaultValue: status });
}
