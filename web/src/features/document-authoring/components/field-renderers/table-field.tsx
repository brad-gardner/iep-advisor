import { useCallback, useRef, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAutosave } from '@/hooks/use-autosave';
import {
  parseConfig,
  readColumnOptions,
  type TableColumn,
} from '@/features/admin/templates/template-config';
import type { TableCellValue } from '../../types';
import { useRegisterFlush } from '../../hooks/flush-registry-context';
import { fieldElementId, type FieldRendererProps } from './types';
import { ROW_BLOCK_SEMANTICS, type ColumnSemantic, type FieldSemantic } from '@/features/admin/templates/document-semantics';
import type { AssistKind } from '../../api/assist-types';
import { FieldAssistBar } from './field-assist-bar';
import { adoptRowIds, carriedFrom, coerceRows, emptyCells, nextRowKey, rowId, type KeyedRow } from '../../lib/table-rows';
import { ROW_CONFIRMED_KEY } from '@/features/admin/templates/document-semantics';
import { useDocumentEditorContext } from '../../hooks/document-editor-context';

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
  const editor = useDocumentEditorContext();

  // `rowsRef` is the single source of truth and is written synchronously by
  // every mutation, so a save always sends the LATEST rows (including any
  // `_rowId`s adopted since the edit was queued) and no update is derived from
  // a stale closure. `rows` state mirrors it for rendering.
  const [rows, setRows] = useState<KeyedRow[]>(() => coerceRows(value));
  const rowsRef = useRef<KeyedRow[]>(rows);
  const mutate = useCallback((updater: (current: KeyedRow[]) => KeyedRow[]) => {
    const next = updater(rowsRef.current);
    rowsRef.current = next;
    setRows(next);
  }, []);

  // The queued autosave value is only a trigger: the payload is read from
  // rowsRef at send time, and ids come back matched to the rows that were sent.
  const autosave = useAutosave<number>(
    useCallback(async () => {
      const sent = rowsRef.current;
      const result = await onSave({ [field.fieldKey]: sent.map((r) => r.cells) });
      const saved = result.values?.[field.fieldKey];
      if (saved !== undefined) mutate((current) => adoptRowIds(current, sent, saved));
    }, [field.fieldKey, onSave, mutate])
  );
  useRegisterFlush(field.fieldKey, autosave.flush);

  const saveSeq = useRef(0);
  const commit = (updater: (current: KeyedRow[]) => KeyedRow[], immediate: boolean) => {
    mutate(updater);
    saveSeq.current += 1;
    autosave.save(saveSeq.current); // value is only a trigger; rowsRef is the payload
    if (immediate) void autosave.flush();
  };

  // Editing a carried-forward row counts as reviewing it: the stale flag clears.
  const updateCell = (rowKey: string, columnKey: string, cell: TableCellValue) =>
    commit(
      (current) =>
        current.map((r) =>
          r.key === rowKey
            ? { ...r, cells: { ...r.cells, [columnKey]: cell, ...(carriedFrom(r) && !r.cells[ROW_CONFIRMED_KEY] ? { [ROW_CONFIRMED_KEY]: true } : {}) } }
            : r
        ),
      false
    );

  const keepRow = (rowKey: string) =>
    commit((current) => current.map((r) => (r.key === rowKey ? { ...r, cells: { ...r.cells, [ROW_CONFIRMED_KEY]: true } } : r)), true);

  const addRow = () => commit((current) => [...current, { key: nextRowKey(), cells: emptyCells(columns) }], true);
  const removeRow = (rowKey: string) => commit((current) => current.filter((r) => r.key !== rowKey), true);

  const atMax = maxRows != null && rows.length >= maxRows;
  const atMin = minRows != null && rows.length <= minRows;

  // AI help per row is offered for semantic row blocks (goals, services, …).
  // The suggestion lands in the row's primary text column; goals also offer
  // "Pull from student" and the measurement kind.
  const blockSemantic = config.kind === 'Table' ? config.semantic : undefined;
  const isRowBlock = blockSemantic != null && ROW_BLOCK_SEMANTICS.has(blockSemantic);
  const primaryColumn =
    columns.find((c) =>
      blockSemantic === 'goals'
        ? c.semantic === 'goalText'
        : blockSemantic === 'services'
          ? c.semantic === 'serviceType'
          : blockSemantic === 'accommodations'
            ? c.semantic === 'accommodation'
            : blockSemantic === 'transition'
              ? c.semantic === 'transitionServices'
              : false
    ) ?? columns.find((c) => c.type === 'Text');
  const rowKinds: AssistKind[] = blockSemantic === 'goals' ? ['Rewrite', 'Improve', 'SuggestMeasurement'] : ['Rewrite', 'Improve'];

  // Semantic row blocks (goals, services, accommodations, …) render as stacked
  // cards with labelled inputs — a goal has six fields and does not fit a
  // grid inside the editor column — with AI help and "pull from student" per
  // row. Untagged tables keep the compact grid.
  if (isRowBlock) {
    const primaryKey = primaryColumn?.columnKey;
    return (
      <div id={fieldElementId(field.id)} tabIndex={-1} role="group" aria-labelledby={labelId} data-testid={`field-${field.fieldKey}`}>
        <div id={labelId} className="mb-2 block text-[13px] font-medium text-brand-slate-600">
          {field.label || 'Untitled field'}
          {field.required && (
            <>
              <span className="ml-1 text-brand-danger-700" aria-hidden="true">
                *
              </span>
              <span className="sr-only"> (required)</span>
            </>
          )}
        </div>
        {rows.length === 0 ? (
          <p className="mb-2 text-sm text-brand-slate-400">No rows yet.</p>
        ) : (
          <ol className="space-y-3">
            {rows.map((row, rowIndex) => {
              const persistedId = rowId(row);
              return (
                <li
                  key={row.key}
                  className="rounded-card border border-brand-slate-200 bg-brand-slate-50/60 p-4"
                  data-testid={`field-${field.fieldKey}-row-${rowIndex}`}
                >
                  <div className="mb-3 flex items-start justify-between gap-3">
                    <span className="flex min-w-0 flex-wrap items-center gap-2 text-[13px] font-medium text-brand-slate-500">
                      {blockLabel(blockSemantic)} {rowIndex + 1}
                      {carriedFrom(row) && (
                        <span
                          className={
                            row.cells[ROW_CONFIRMED_KEY] === true
                              ? 'rounded-full border border-brand-slate-200 px-2 py-0.5 text-[11px] font-normal text-brand-slate-500'
                              : 'rounded-full border border-brand-amber-200 bg-brand-amber-50 px-2 py-0.5 text-[11px] font-normal text-brand-amber-700'
                          }
                          data-testid={`field-${field.fieldKey}-row-${rowIndex}-carried`}
                        >
                          Carried from {carriedFrom(row)!.label ?? 'prior version'}
                          {carriedFrom(row)!.date ? ` (${carriedFrom(row)!.date})` : ''}
                          {row.cells[ROW_CONFIRMED_KEY] === true ? ' · reviewed' : ' · not yet reviewed'}
                        </span>
                      )}
                    </span>
                    <span className="flex shrink-0 items-center gap-1">
                      {carriedFrom(row) && row.cells[ROW_CONFIRMED_KEY] !== true && !disabled && (
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => keepRow(row.key)}
                          data-testid={`field-${field.fieldKey}-row-${rowIndex}-keep`}
                        >
                          Keep as-is
                        </Button>
                      )}
                      <Button
                        variant="danger"
                        size="sm"
                        disabled={disabled || atMin}
                        onClick={() => removeRow(row.key)}
                        aria-label={`Remove ${blockLabel(blockSemantic).toLowerCase()} ${rowIndex + 1}`}
                        data-testid={`field-${field.fieldKey}-remove-${rowIndex}`}
                      >
                        <Trash2 className="h-4 w-4" aria-hidden="true" />
                      </Button>
                    </span>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    {columns.map((col) => {
                      const wide = col.columnKey === primaryKey || col.type === 'Text' && isLongColumn(col.semantic);
                      const cellId = `field-${field.fieldKey}-cell-${rowIndex}-${col.columnKey}`;
                      return (
                        <div key={col.columnKey} className={wide ? 'sm:col-span-2' : undefined}>
                          <label htmlFor={cellId} className="mb-1 block text-[13px] font-medium text-brand-slate-600">
                            {col.label || 'Column'}
                            {col.required && (
                              <span className="ml-1 text-brand-danger-700" aria-hidden="true">
                                *
                              </span>
                            )}
                          </label>
                          <TableCell
                            column={col}
                            rowIndex={rowIndex}
                            fieldKey={field.fieldKey}
                            value={row.cells[col.columnKey]}
                            disabled={disabled}
                            multiline={wide}
                            inputId={cellId}
                            onChange={(cell) => updateCell(row.key, col.columnKey, cell)}
                            onFocus={
                              col.type === 'Text'
                                ? () =>
                                    editor?.setActiveField({
                                      label: `${blockLabel(blockSemantic)} ${rowIndex + 1} — ${col.label || 'field'}`,
                                      apply: (text) => {
                                        updateCell(row.key, col.columnKey, text);
                                        void autosave.flush();
                                      },
                                    })
                                : undefined
                            }
                            onBlur={() => void autosave.flush()}
                          />
                        </div>
                      );
                    })}
                  </div>
                  {primaryColumn && persistedId ? (
                    <FieldAssistBar
                      fieldKey={field.fieldKey}
                      rowId={persistedId}
                      kinds={rowKinds}
                      allowPull={blockSemantic === 'goals'}
                      onApply={(text) => {
                        updateCell(row.key, primaryColumn.columnKey, text);
                        void autosave.flush();
                      }}
                      beforeRequest={autosave.flush}
                      disabled={disabled}
                      testIdPrefix={`field-${field.fieldKey}-row-${rowIndex}`}
                    />
                  ) : (
                    !disabled && (
                      <p className="mt-2 text-xs text-brand-slate-400">AI help is available once this row has saved.</p>
                    )
                  )}
                </li>
              );
            })}
          </ol>
        )}
        <div className="mt-3">
          <Button
            variant="secondary"
            size="sm"
            disabled={disabled || atMax}
            onClick={addRow}
            data-testid={`field-${field.fieldKey}-add`}
          >
            <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
            Add {blockLabel(blockSemantic).toLowerCase()}
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div id={fieldElementId(field.id)} tabIndex={-1} role="group" aria-labelledby={labelId}>
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
                        onChange={(cell) => updateCell(row.key, col.columnKey, cell)}
                        onBlur={() => void autosave.flush()}
                      />
                    </td>
                  ))}
                  <td className="px-2 py-1.5 text-right align-top">
                    <Button
                      variant="danger"
                      size="sm"
                      disabled={disabled || atMin}
                      onClick={() => removeRow(row.key)}
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

/** Human label for one row of a semantic block ("Goal 2", "Service 1"). */
function blockLabel(semantic: FieldSemantic | undefined): string {
  switch (semantic) {
    case 'goals':
      return 'Goal';
    case 'services':
      return 'Service';
    case 'accommodations':
      return 'Accommodation';
    case 'transition':
      return 'Transition item';
    case 'participants':
      return 'Participant';
    case 'evaluatorReports':
      return 'Evaluator report';
    default:
      return 'Row';
  }
}

/** Columns whose content is prose and deserves a full-width multiline input. */
function isLongColumn(semantic: ColumnSemantic | undefined): boolean {
  return semantic === 'goalText' || semantic === 'baseline' || semantic === 'targetCriteria' || semantic === 'findings' || semantic === 'transitionServices' || semantic === 'accommodation';
}

function TableCell({
  column,
  rowIndex,
  fieldKey,
  value,
  disabled,
  multiline,
  inputId,
  onFocus,
  onChange,
  onBlur,
}: {
  column: TableColumn;
  rowIndex: number;
  fieldKey: string;
  value: TableCellValue | undefined;
  disabled?: boolean;
  /** Block mode: render prose Text columns as a textarea. */
  multiline?: boolean;
  /** Block mode: explicit id so the visible label associates with the control. */
  inputId?: string;
  onFocus?: () => void;
  onChange: (cell: TableCellValue) => void;
  // Flush the field's pending debounced save when the cell loses focus, so an
  // in-app navigation that blurs the cell persists the edit before unmount.
  onBlur: () => void;
}) {
  const ariaLabel = inputId ? undefined : `${column.label || 'Column'}, row ${rowIndex + 1}`;
  const testId = `field-${fieldKey}-cell-${rowIndex}-${column.columnKey}`;
  const strValue = typeof value === 'string' ? value : '';

  if (multiline && column.type === 'Text') {
    return (
      <textarea
        id={inputId}
        rows={2}
        value={strValue}
        disabled={disabled}
        aria-label={ariaLabel}
        onFocus={onFocus}
        onChange={(e) => onChange(e.target.value)}
        onBlur={onBlur}
        className={cellInputClass}
        data-testid={testId}
      />
    );
  }

  switch (column.type) {
    case 'Checkbox':
      return (
        <input
          id={inputId}
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
          id={inputId}
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
          id={inputId}
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
          id={inputId}
          type="text"
          value={strValue}
          disabled={disabled}
          aria-label={ariaLabel}
          onFocus={onFocus}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cellInputClass}
          data-testid={testId}
        />
      );
  }
}
