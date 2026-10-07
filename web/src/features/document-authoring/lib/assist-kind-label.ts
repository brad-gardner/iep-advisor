import i18n from '@/lib/i18n';
import type { AssistKind } from '../api/assist-types';

// Human-facing label for an AI-assist request kind
// (`document-authoring:assistKind.*`) — DISPLAY ONLY. The stored `AssistKind`
// value stays the canonical English string regardless of the active
// language (sent to the API). A plain function (not a hook), same shape as
// `orgRoleLabel`/`gradeLevelLabel` in `src/lib`.
export function assistKindLabel(kind: AssistKind): string {
  return i18n.t(`document-authoring:assistKind.${kind}`, { defaultValue: kind });
}
