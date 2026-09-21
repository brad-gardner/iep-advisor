import { memo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { FieldValueDisplay } from '@/features/document-authoring/components/authored-version-snapshot';
import type { TemplateFieldDto, TemplateVersionDetailDto } from '@/features/admin/templates/types';
import { DraftItemCard } from './draft-item-card';
import { ExplainPanel } from './explain-panel';
import {
  coerceRows,
  isCardRowField,
  primaryColumnKey,
  rowId as readRowId,
  rowLabel,
  tableColumns,
} from '../lib/semantic-rows';
import type { ChangeRowDto, ChangeSummaryDto } from '../types';

interface FrozenSectionListProps {
  revisionId: number;
  canRespond: boolean;
  templateVersion: TemplateVersionDetailDto;
  values: Record<string, unknown>;
  changeSummary: ChangeSummaryDto | null;
}

/**
 * Memoised: its props are the frozen revision (stable references), so the
 * parent page's own state changes (a note added, a response sent) don't
 * re-render every section — only the context consumers that care do.
 *
 * Reads the frozen template tree the same way `AuthoredVersionSnapshot` does,
 * but a goal/service/accommodation table renders each row as an interactive
 * `DraftItemCard` (Explain / Ask a question / Respond) instead of a plain
 * `<table>`. Every other field falls back to the shared generic renderer.
 */
export const FrozenSectionList = memo(function FrozenSectionList({
  revisionId,
  canRespond,
  templateVersion,
  values,
  changeSummary,
}: FrozenSectionListProps) {
  const sections = [...templateVersion.sections].sort((a, b) => a.displayOrder - b.displayOrder);
  if (sections.length === 0) {
    return <p className="text-sm text-brand-slate-500">This document has no sections.</p>;
  }

  const changedFieldKeys = new Set((changeSummary?.changedFields ?? []).map((f) => f.fieldKey));
  const addedRowKeys = new Set((changeSummary?.addedRows ?? []).map((r) => `${r.fieldKey}:${r.rowId}`));
  const changedRowKeys = new Set((changeSummary?.changedRows ?? []).map((r) => `${r.fieldKey}:${r.rowId}`));
  const removedByField = groupByFieldKey(changeSummary?.removedRows ?? []);

  return (
    <div className="space-y-6" data-testid="frozen-section-list">
      {sections.map((section) => {
        const fields = [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder);
        const rowBlockFields = fields.filter((f) => isCardRowField(f) !== null);
        const otherFields = fields.filter((f) => isCardRowField(f) === null);

        return (
          <section key={section.id} data-testid={`frozen-section-${section.id}`}>
            <h2 className="mb-1 font-serif text-lg text-brand-slate-800">{section.title || 'Untitled section'}</h2>
            {/* Section-level plain-language explanation — the only Explain affordance for
                narrative fields (Present Levels etc.), which have no per-item cards. */}
            <div className="mb-3">
              <ExplainPanel target={{ kind: 'section', sectionId: section.id, title: section.title }} data-testid={`explain-section-${section.id}`} />
            </div>

            {otherFields.length > 0 && (
              <Card className="mb-4 space-y-4">
                {otherFields.map((field) => (
                  <div key={field.id}>
                    {changedFieldKeys.has(field.fieldKey) && (
                      <Badge variant="warning" className="mb-1">
                        Updated since last revision
                      </Badge>
                    )}
                    <FieldValueDisplay field={field} value={values[field.fieldKey]} />
                  </div>
                ))}
              </Card>
            )}

            {rowBlockFields.map((field) => (
              <RowBlockGroup
                key={field.id}
                revisionId={revisionId}
                canRespond={canRespond}
                field={field}
                value={values[field.fieldKey]}
                removedRows={removedByField.get(field.fieldKey) ?? []}
                addedRowKeys={addedRowKeys}
                changedRowKeys={changedRowKeys}
              />
            ))}
          </section>
        );
      })}
    </div>
  );
});

function RowBlockGroup({
  revisionId,
  canRespond,
  field,
  value,
  removedRows,
  addedRowKeys,
  changedRowKeys,
}: {
  revisionId: number;
  canRespond: boolean;
  field: TemplateFieldDto;
  value: unknown;
  removedRows: ChangeRowDto[];
  addedRowKeys: Set<string>;
  changedRowKeys: Set<string>;
}) {
  const semantic = isCardRowField(field);
  if (!semantic) return null;
  const rows = coerceRows(value);
  const columns = tableColumns(field);
  const pKey = primaryColumnKey(field, semantic);

  return (
    <div className="mb-4 space-y-3">
      <h3 className="text-sm font-medium text-brand-slate-600">{field.label || 'Untitled field'}</h3>

      {removedRows.length > 0 && (
        <ul className="space-y-1 text-sm text-brand-slate-500" data-testid={`removed-rows-${field.fieldKey}`}>
          {removedRows.map((r) => (
            <li key={r.rowId}>Removed since last revision: {r.label}</li>
          ))}
        </ul>
      )}

      {rows.length === 0 ? (
        <p className="text-sm text-brand-slate-500">Nothing here yet.</p>
      ) : (
        <div className="space-y-3">
          {rows.map((row, i) => {
            const id = readRowId(row);
            const key = `${field.fieldKey}:${id ?? i}`;
            const changeState = id && addedRowKeys.has(`${field.fieldKey}:${id}`)
              ? 'added'
              : id && changedRowKeys.has(`${field.fieldKey}:${id}`)
                ? 'changed'
                : null;
            const otherColumns = columns
              .filter((c) => c.columnKey !== pKey)
              .map((c) => ({ column: c, value: row[c.columnKey] }));
            return (
              <DraftItemCard
                key={key}
                revisionId={revisionId}
                canRespond={canRespond}
                fieldKey={field.fieldKey}
                rowId={id}
                label={rowLabel(row, pKey, i)}
                otherColumns={otherColumns}
                changeState={changeState}
              />
            );
          })}
        </div>
      )}
    </div>
  );
}

function groupByFieldKey(rows: ChangeRowDto[]): Map<string, ChangeRowDto[]> {
  const map = new Map<string, ChangeRowDto[]>();
  for (const row of rows) {
    const list = map.get(row.fieldKey) ?? [];
    list.push(row);
    map.set(row.fieldKey, list);
  }
  return map;
}
