import type { TemplateFieldDto } from '@/features/admin/templates/types';
import type { DocumentValuePatch } from '../../types';

/** Shared props for every per-`FieldType` renderer. Each reads its value from
 *  the instance's values map (by `fieldKey`) and debounces a patch save. */
export interface FieldRendererProps {
  field: TemplateFieldDto;
  /** Raw value for this field from the value-document (may be undefined). */
  value: unknown;
  disabled?: boolean;
  /** Persist a value patch — the instance hook serializes + threads rowVersion. */
  onSave: (patch: DocumentValuePatch) => Promise<unknown>;
}

/** Stable DOM id for a field's control (label association / test hooks). */
export function fieldElementId(fieldId: number): string {
  return `document-field-${fieldId}`;
}
