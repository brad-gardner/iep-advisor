import { parseConfig, readColumnOptions, type TableColumn } from '@/features/admin/templates/template-config';
import { OWNER_ELIGIBLE_SEMANTICS, type FieldSemantic } from '@/features/admin/templates/document-semantics';
import { formatDate } from '@/lib/format-date';
import { useDocumentEditorContext } from '../../../hooks/document-editor-context';
import { coerceRows, ownerUserId } from '../../../lib/table-rows';
import { resolveOwnerDisplay } from '../../../lib/owner-display';
import type { TableCellValue } from '../../../types';
import { fieldElementId } from '../types';
import { ReadAccommodations } from './read-accommodations';
import { ReadTransitionList } from './read-transition-list';
import type { ReadFieldRendererProps } from './types';

/**
 * Read view for a Table field. Accommodations and transition rows get their own
 * grouped layout (`ReadAccommodations` / `ReadTransitionList`); everything else —
 * including goals and services until their Phase 3/4 card/schedule views land —
 * falls through to this generic one-row-per-entry table, which appends an Owner
 * column for any owner-eligible semantic (plan 2026-10-02-002).
 */
export function ReadTable(props: ReadFieldRendererProps) {
  const { field } = props;
  const config = parseConfig(field.fieldType, field.configJson);
  const semantic = config.kind === 'Table' ? config.semantic : undefined;

  if (semantic === 'accommodations') return <ReadAccommodations {...props} />;
  if (semantic === 'transition') return <ReadTransitionList {...props} />;
  return <ReadTableGeneric {...props} semantic={semantic} />;
}

function ReadTableGeneric({ field, value, semantic }: ReadFieldRendererProps & { semantic: FieldSemantic | undefined }) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const rows = coerceRows(value);
  const editor = useDocumentEditorContext();
  const ownerEligible = semantic != null && OWNER_ELIGIBLE_SEMANTICS.has(semantic);

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
      {rows.length === 0 ? (
        <p className="mt-0.5 text-[15px] italic text-brand-slate-500">No rows yet.</p>
      ) : (
        <div className="mt-1.5 overflow-x-auto rounded-card border border-brand-slate-200">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">{field.label || 'Table field'}</caption>
            <thead>
              <tr className="bg-brand-slate-50">
                {columns.map((c) => (
                  <th
                    key={c.columnKey}
                    scope="col"
                    className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600"
                  >
                    {c.label || 'Column'}
                  </th>
                ))}
                {ownerEligible && (
                  <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                    Owner
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const owner = ownerEligible ? resolveOwnerDisplay(ownerUserId(row), editor?.team) : null;
                return (
                  <tr key={row.key} className="border-b border-brand-slate-100 last:border-0">
                    {columns.map((col) => (
                      <td key={col.columnKey} className="px-3 py-2 align-top text-brand-slate-700">
                        {formatCell(col, row.cells[col.columnKey])}
                      </td>
                    ))}
                    {ownerEligible && (
                      <td className="px-3 py-2 align-top text-brand-slate-700">
                        {owner ? (
                          <span className={owner.former ? 'italic text-brand-slate-500' : undefined}>{owner.label}</span>
                        ) : (
                          '—'
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function formatCell(column: TableColumn, value: TableCellValue | undefined): string {
  if (column.type === 'Checkbox') return value === true ? 'Yes' : 'No';
  if (column.type === 'Date') return typeof value === 'string' && value ? formatDate(value) : '—';
  if (column.type === 'Select') {
    const str = typeof value === 'string' ? value : '';
    if (!str) return '—';
    const label = readColumnOptions(column.configJson).find((o) => o.value === str)?.label?.trim();
    return label || str;
  }
  return typeof value === 'string' && value.trim() ? value : '—';
}
