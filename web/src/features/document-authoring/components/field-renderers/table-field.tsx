import { useCallback, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAutosave } from '@/features/iep-authoring/hooks/use-autosave';
import {
  parseConfig,
  readColumnOptions,
  type TableColumn,
} from '@/features/admin/templates/template-config';
import type { TableCellValue, TableRowValue } from '../../types';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { fieldElementId, type FieldRendererProps } from './types';

/** Row wrapper carrying a stable client key so add/remove keeps React identity
 *  (and focus/pending edits) pinned to the logical row, not its position. */
interface KeyedRow {
  key: string;
  cells: TableRowValue;
}

let rowSeq = 0;
function nextKey(): string {
  rowSeq += 1;
  return `row-${rowSeq}`;
}

function coerceRows(value: unknown): KeyedRow[] {
  if (!Array.isArray(value)) return [];
  return value
    .filter((r): r is TableRowValue => typeof r === 'object' && r !== null)
    .map((cells) => ({ key: nextKey(), cells }));
}

function emptyCells(columns: TableColumn[]): TableRowValue {
  const row: TableRowValue = {};
  for (const col of columns) row[col.columnKey] = col.type === 'Checkbox' ? false : '';
  return row;
}

/**
 * Repeating-group Table field: one row per array entry, one cell input per
 * column. The whole array is a single value in the value-document, so every
 * edit/add/remove saves the full array under the field's key. Add/remove respect
 * the config's min/max rows for control enablement but never hard-block a
 * partial draft (validation is enforced at finalize in Phase 4).
 */
export function TableField({ field, value, disabled, onSave }: FieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const table =
    config.kind === 'Table' ? config.table : { columns: [], minRows: undefined, maxRows: undefined };
  const { columns, minRows, maxRows } = table;
  const labelId = `${fieldElementId(field.id)}-label`;

  const [rows, setRows] = useState<KeyedRow[]>(() => coerceRows(value));
  const autosave = useAutosave<TableRowValue[]>(
    useCallback(async (v) => void (await onSave({ [field.fieldKey]: v })), [field.fieldKey, onSave])
  );
  useRegisterFlush(field.fieldKey, autosave.flush);

  const commit = (next: KeyedRow[], immediate: boolean) => {
    setRows(next);
    autosave.save(next.map((r) => r.cells));
    if (immediate) void autosave.flush();
  };

  const updateCell = (rowIndex: number, columnKey: string, cell: TableCellValue) => {
    const next = rows.map((r, i) =>
      i === rowIndex ? { ...r, cells: { ...r.cells, [columnKey]: cell } } : r
    );
    commit(next, false);
  };

  const addRow = () => commit([...rows, { key: nextKey(), cells: emptyCells(columns) }], true);
  const removeRow = (rowIndex: number) => commit(rows.filter((_, i) => i !== rowIndex), true);

  const atMax = maxRows != null && rows.length >= maxRows;
  const atMin = minRows != null && rows.length <= minRows;

  return (
    <div role="group" aria-labelledby={labelId}>
      <div id={labelId} className="mb-1 block text-[13px] font-medium text-brand-slate-600">
        {field.label || 'Untitled field'}
        {field.required && (
          <span className="ml-1 text-brand-danger-700" aria-hidden="true">
            *
          </span>
        )}
        {field.required && <span className="sr-only"> (required)</span>}
      </div>
      <div
        className="overflow-x-auto rounded-card border border-brand-slate-200"
        data-testid={`field-${field.fieldKey}`}
      >
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">{field.label || 'Table field'}</caption>
          <thead>
            <tr className="bg-brand-slate-50">
              {columns.map((c) => (
                <th
                  key={c.columnKey}
                  scope="col"
                  className="border-b border-brand-slate-200 px-2 py-2 text-left text-[13px] font-medium text-brand-slate-600"
                >
                  {c.label || 'Column'}
                  {c.required && (
                    <>
                      <span className="ml-1 text-brand-danger-700" aria-hidden="true">
                        *
                      </span>
                      <span className="sr-only"> (required)</span>
                    </>
                  )}
                </th>
              ))}
              <th scope="col" className="w-12 border-b border-brand-slate-200 px-2 py-2">
                <span className="sr-only">Row actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr>
                <td
                  colSpan={columns.length + 1}
                  className="px-3 py-4 text-center text-sm text-brand-slate-400"
                >
                  No rows yet.
                </td>
              </tr>
            ) : (
              rows.map((row, rowIndex) => (
                <tr key={row.key} className="border-b border-brand-slate-100 last:border-0">
                  {columns.map((col) => (
                    <td key={col.columnKey} className="px-2 py-1.5 align-top">
                      <TableCell
                        column={col}
                        rowIndex={rowIndex}
                        fieldKey={field.fieldKey}
                        value={row.cells[col.columnKey]}
                        disabled={disabled}
                        onChange={(cell) => updateCell(rowIndex, col.columnKey, cell)}
                        onBlur={() => void autosave.flush()}
                      />
                    </td>
                  ))}
                  <td className="px-2 py-1.5 text-right align-top">
                    <Button
                      variant="danger"
                      size="sm"
                      disabled={disabled || atMin}
                      onClick={() => removeRow(rowIndex)}
                      aria-label={`Remove row ${rowIndex + 1}`}
                      data-testid={`field-${field.fieldKey}-remove-${rowIndex}`}
                    >
                      <Trash2 className="h-4 w-4" aria-hidden="true" />
                    </Button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <div className="mt-2">
        <Button
          variant="secondary"
          size="sm"
          disabled={disabled || atMax}
          onClick={addRow}
          data-testid={`field-${field.fieldKey}-add`}
        >
          <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
          Add row
        </Button>
      </div>
    </div>
  );
}

const cellInputClass =
  'w-full px-2 py-1 bg-white rounded-input text-brand-slate-800 text-sm border border-brand-slate-200 focus:outline-none focus:border-brand-teal-400 focus:ring-[3px] focus:ring-brand-teal-50 transition-colors';

function TableCell({
  column,
  rowIndex,
  fieldKey,
  value,
  disabled,
  onChange,
  onBlur,
}: {
  column: TableColumn;
  rowIndex: number;
  fieldKey: string;
  value: TableCellValue | undefined;
  disabled?: boolean;
  onChange: (cell: TableCellValue) => void;
  // Flush the field's pending debounced save when the cell loses focus, so an
  // in-app navigation that blurs the cell persists the edit before unmount.
  onBlur: () => void;
}) {
  const ariaLabel = `${column.label || 'Column'}, row ${rowIndex + 1}`;
  const testId = `field-${fieldKey}-cell-${rowIndex}-${column.columnKey}`;
  const strValue = typeof value === 'string' ? value : '';

  switch (column.type) {
    case 'Checkbox':
      return (
        <input
          type="checkbox"
          checked={value === true}
          disabled={disabled}
          aria-label={ariaLabel}
          onChange={(e) => onChange(e.target.checked)}
          onBlur={onBlur}
          className="h-4 w-4 rounded border-brand-slate-300 text-brand-teal-500 focus:ring-brand-teal-400"
          data-testid={testId}
        />
      );
    case 'Date':
      return (
        <input
          type="date"
          value={strValue}
          disabled={disabled}
          aria-label={ariaLabel}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cellInputClass}
          data-testid={testId}
        />
      );
    case 'Select':
      return (
        <select
          value={strValue}
          disabled={disabled}
          aria-label={ariaLabel}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cellInputClass}
          data-testid={testId}
        >
          <option value="">Select…</option>
          {readColumnOptions(column.configJson).map((o) => (
            <option key={o.value} value={o.value}>
              {o.label?.trim() || o.value}
            </option>
          ))}
        </select>
      );
    default:
      return (
        <input
          type="text"
          value={strValue}
          disabled={disabled}
          aria-label={ariaLabel}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cellInputClass}
          data-testid={testId}
        />
      );
  }
}
