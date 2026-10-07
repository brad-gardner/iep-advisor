import i18n from '@/lib/i18n';
import type { GoalRecordStatus } from '../types';

/**
 * Translated label for a goal record's status (`goals:status.*`), following
 * `orgRoleLabel`'s shape: a plain function over `i18n.t`, callable from
 * render bodies and plain code alike. Every `GoalRecordStatus` has a
 * translation, so there is no fallback to pass — an unrecognized value is a
 * `tsc` error at the call site, not a runtime concern.
 */
export function goalStatusLabel(status: GoalRecordStatus): string {
  return i18n.t(`goals:status.${status}`);
}
