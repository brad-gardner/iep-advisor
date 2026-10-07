import i18n from '@/lib/i18n';
import type { DocumentInstanceStatus } from '../types';

// Human-facing label for a draft document instance's lifecycle status
// (`document-authoring:documentStatus.*`) — DISPLAY ONLY. The stored
// `DocumentInstanceStatus` value stays the canonical English string
// regardless of the active language. A plain function (not a hook), same
// shape as `orgRoleLabel`/`gradeLevelLabel` in `src/lib`. Callers that need
// the status embedded mid-sentence (e.g. "it is currently {{status}}") pass
// `.toLowerCase()` on the result, same as the original hard-coded English
// did — correct in both languages, since both use sentence case already.
export function documentStatusLabel(status: DocumentInstanceStatus): string {
  return i18n.t(`document-authoring:documentStatus.${status}`, { defaultValue: status });
}
