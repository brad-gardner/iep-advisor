import i18n from '@/lib/i18n';
import type { JournalTag } from '../types/journal';

/**
 * Translated label for a journal tag (`journal:tag.*`), following
 * `orgRoleLabel`'s shape: a plain function over `i18n.t`, callable from
 * render bodies and plain code alike. `tag` is typed as `JournalTag` so a
 * typo at the call site is still a `tsc` error, but the value itself comes
 * from stored/server data that can outlive the client's known set (a tag
 * added server-side before this client updates), so a lookup miss falls
 * back to the raw value via `defaultValue` rather than showing a raw
 * `journal:tag.*` key.
 */
export function journalTagLabel(tag: JournalTag): string {
  return i18n.t(`journal:tag.${tag}`, { defaultValue: tag });
}
