import { parseConfig, readColumnOptions, type TableColumn } from '@/features/admin/templates/template-config';
import { formatDate } from '@/lib/format-date';
import { coerceRows } from '../../../lib/table-rows';
import type { TableCellValue } from '../../../types';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

/**
 * Generic read view for a Table field: one row per entry, one column per
 * configured column. Semantic row blocks (goals, services, …) get their own
 * card/schedule views in later phases — this is the shared fallback that every
 * Table renders with today.
 */
export function ReadTable({ field, value }: ReadFieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const rows = coerceRows(value);

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
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.key} className="border-b border-brand-slate-100 last:border-0">
                  {columns.map((col) => (
                    <td key={col.columnKey} className="px-3 py-2 align-top text-brand-slate-700">
                      {formatCell(col, row.cells[col.columnKey])}
                    </td>
                  ))}
                </tr>
              ))}
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
