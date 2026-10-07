import i18n from '@/lib/i18n';
import type { SignatureStatus } from '../types';

// Human-facing label for a finalized version's print/sign status
// (`document-authoring:signatureStatus.*`) — DISPLAY ONLY. The stored
// `SignatureStatus` value stays the canonical English string regardless of
// the active language (sent to/from the API, used for comparisons). A plain
// function (not a hook) so it can be called from both a component render and
// anywhere else that needs the label, same shape as `orgRoleLabel`/
// `gradeLevelLabel` in `src/lib`.
export function signatureStatusLabel(status: SignatureStatus): string {
  return i18n.t(`document-authoring:signatureStatus.${status}`, { defaultValue: status });
}
