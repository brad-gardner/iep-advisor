import i18n from '@/lib/i18n';
import type { JournalTag } from '../types/journal';

/**
 * Translated label for a journal tag (`journal:tag.*`), following
 * `orgRoleLabel`'s shape: a plain function over `i18n.t`, callable from
 * render bodies and plain code alike. Every `JournalTag` has a translation,
 * so there is no fallback to pass — an unrecognized value is a `tsc` error
 * at the call site, not a runtime concern.
 */
export function journalTagLabel(tag: JournalTag): string {
  return i18n.t(`journal:tag.${tag}`);
}
