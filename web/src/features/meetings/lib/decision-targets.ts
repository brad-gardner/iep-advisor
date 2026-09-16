import { coerceRows, isCardRowField, primaryColumnKey, rowLabel } from '@/features/shared-drafts/lib/semantic-rows';
import type { DocumentInstanceDetailDto } from '@/features/document-authoring/types';

export interface DecisionTargetOption {
  fieldKey: string;
  rowId: string;
  /** e.g. "Annual goals: Reading fluency by June 2027". */
  label: string;
}

/**
 * Goals/services rows from a linked draft's Table fields, as target-picker
 * options for a meeting decision (plan 7, decision 3: "target picker: optional
 * field/row from the linked draft's goals/services rows"). Only rows that
 * already carry a `_rowId` are offered — a row without one can't be targeted
 * reliably. Reuses the same semantics machinery the parent Converge view uses
 * to render goals/services as cards, rather than re-deriving it.
 */
export function buildDecisionTargets(detail: DocumentInstanceDetailDto): DecisionTargetOption[] {
  const options: DecisionTargetOption[] = [];
  for (const section of detail.templateVersion.sections) {
    for (const field of section.fields) {
      const semantic = isCardRowField(field);
      if (!semantic) continue;
      const primaryKey = primaryColumnKey(field, semantic);
      const rows = coerceRows(detail.values[field.fieldKey]);
      rows.forEach((row, index) => {
        const id = row._rowId;
        if (typeof id !== 'string' || !id) return;
        options.push({
          fieldKey: field.fieldKey,
          rowId: id,
          label: `${field.label}: ${rowLabel(row, primaryKey, index)}`,
        });
      });
    }
  }
  return options;
}
