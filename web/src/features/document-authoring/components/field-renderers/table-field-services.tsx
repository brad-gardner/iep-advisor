import { useEffect, useRef, useState } from 'react';
import { Plus } from 'lucide-react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import type { TableColumn } from '@/features/admin/templates/template-config';
import { ROW_CONFIRMED_KEY } from '@/features/admin/templates/document-semantics';
import { Button } from '@/components/ui/button';
import type { AutosaveStatus } from '@/hooks/use-autosave';
import { AutosaveIndicator } from '@/features/admin/templates/components/autosave-indicator';
import { cn } from '@/lib/cn';
import type { AssistKind } from '../../api/assist-types';
import type { StudentTeamCache } from '../../hooks/use-student-team';
import { useDocumentEditorContext } from '../../hooks/document-editor-context';
import { carriedFrom, emptyCells, nextRowKey, ownerUserId, rowId, withOwner, type KeyedRow } from '../../lib/table-rows';
import { columnDisplayLabel } from '../../lib/group-rows';
import { resolveOwnerDisplay } from '../../lib/owner-display';
import { formatCarriedDate, formatDateRange } from '../../lib/table-cell-format';
import {
  formatDurationText,
  formatFrequencyText,
  formatScheduleSummary,
  parseDurationMinutes,
  parseFrequency,
  totalMinutesPerWeek,
  type ServicePeriod,
} from '../../lib/service-schedule';
import type { TableCellValue } from '../../types';
import { TableCell, cellInputClass } from './table-field';
import { FieldAssistBar } from './field-assist-bar';
import { TeamMemberSelect } from '../team-member-select';
import { fieldElementId } from './types';

type CellTarget = (rowKey: string, col: TableColumn) => { id: string; label: () => string; apply: (text: string) => void };

function asText(v: TableCellValue | undefined): string {
  return typeof v === 'string' ? v : '';
}

interface ServicesBlockProps {
  field: TemplateFieldDto;
  columns: TableColumn[];
  rows: KeyedRow[];
  disabled?: boolean;
  atMax: boolean;
  atMin: boolean;
  editorTeam: StudentTeamCache | undefined;
  ownerWarnings: Record<string, string>;
  /** Lands directly in this service's focused editor (a read-row's "Edit"),
   *  instead of defaulting to the compact schedule row. */
  initialFocusRowKey?: string;
  saveStatus: AutosaveStatus;
  commit: (updater: (current: KeyedRow[]) => KeyedRow[], immediate: boolean) => void;
  onKeepRow: (rowKey: string) => void;
  onRemoveRow: (rowKey: string) => void;
  flush: () => Promise<void>;
  cellTarget: CellTarget;
}

function serviceEditButtonDomId(fieldKey: string, rowKey: string): string {
  return `field-${fieldKey}-service-${rowKey}-edit-button`;
}

/**
 * Services block (plan 2026-10-02-002, Phase 4): a schedule table — one
 * compact row per service, one of which may be swapped for the full
 * `ServiceEditor` — mirroring `GoalsBlock`'s card-list-plus-focused-editor
 * split. "+ Add service" always appends (never inserts), for the same reason
 * goals do: a brand-new row must only ever land at the end so id adoption
 * never has to guess which row a server id belongs to.
 */
export function ServicesBlock({
  field,
  columns,
  rows,
  disabled,
  atMax,
  atMin,
  editorTeam,
  ownerWarnings,
  initialFocusRowKey,
  saveStatus,
  commit,
  onKeepRow,
  onRemoveRow,
  flush,
  cellTarget,
}: ServicesBlockProps) {
  const labelId = `${fieldElementId(field.id)}-label`;
  const [focusedRowKey, setFocusedRowKey] = useState<string | null>(() => initialFocusRowKey ?? null);

  const serviceTypeCol = columns.find((c) => c.semantic === 'serviceType') ?? columns.find((c) => c.type === 'Text');
  const providerRoleCol = columns.find((c) => c.semantic === 'providerRole');
  const frequencyCol = columns.find((c) => c.semantic === 'frequency');
  const durationCol = columns.find((c) => c.semantic === 'duration');
  const locationCol = columns.find((c) => c.semantic === 'location');
  const startDateCol = columns.find((c) => c.semantic === 'startDate');
  const endDateCol = columns.find((c) => c.semantic === 'endDate');
  const known = new Set(
    [serviceTypeCol, providerRoleCol, frequencyCol, durationCol, locationCol, startDateCol, endDateCol]
      .filter((c): c is TableColumn => c != null)
      .map((c) => c.columnKey)
  );
  const extraColumns = columns.filter((c) => !known.has(c.columnKey));

  const totals = totalMinutesPerWeek(
    rows.map((row) => ({
      frequencyText: frequencyCol ? asText(row.cells[frequencyCol.columnKey]) : undefined,
      durationText: durationCol ? asText(row.cells[durationCol.columnKey]) : undefined,
    }))
  );

  const addService = () => {
    const key = nextRowKey();
    commit((current) => [...current, { key, cells: emptyCells(columns) }], true);
    setFocusedRowKey(key);
  };

  return (
    <div id={fieldElementId(field.id)} tabIndex={-1} role="group" aria-labelledby={labelId} data-testid={`field-${field.fieldKey}`}>
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <h3 id={labelId} className="text-[13px] font-medium text-brand-slate-600">
          {field.label || 'Services'}
          {field.required && (
            <span className="ml-1 text-brand-danger-700" aria-hidden="true">
              *
            </span>
          )}
        </h3>
        {rows.length > 0 && (
          <span className="text-xs text-brand-slate-500">
            {rows.length} service{rows.length === 1 ? '' : 's'} · {totals.totalMinutesPerWeek} min/week
            {totals.excludedCount > 0 &&
              ` (${totals.excludedCount} not counted — frequency/duration unclear)`}
          </span>
        )}
        <Button
          variant="secondary"
          size="sm"
          className="ml-auto"
          disabled={disabled || atMax}
          onClick={addService}
          data-testid={`field-${field.fieldKey}-add`}
        >
          <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
          Add service
        </Button>
      </div>

      {rows.length === 0 ? (
        <p className="text-sm text-brand-slate-500">No services yet. Add one to get started.</p>
      ) : (
        <div className="overflow-x-auto rounded-card border border-brand-slate-200">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">{field.label || 'Services'}</caption>
            <thead>
              <tr className="bg-brand-slate-50">
                <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                  Service
                </th>
                <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                  Frequency
                </th>
                <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                  Setting
                </th>
                <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                  Dates
                </th>
                <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                  Owner
                </th>
                <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                  <span className="sr-only">Row actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) =>
                row.key === focusedRowKey ? (
                  <ServiceEditorRow
                    key={row.key}
                    field={field}
                    row={row}
                    index={index}
                    disabled={disabled}
                    atMin={atMin}
                    editorTeam={editorTeam}
                    ownerWarning={rowId(row) ? ownerWarnings[rowId(row) as string] : undefined}
                    saveStatus={saveStatus}
                    serviceTypeCol={serviceTypeCol}
                    providerRoleCol={providerRoleCol}
                    frequencyCol={frequencyCol}
                    durationCol={durationCol}
                    locationCol={locationCol}
                    startDateCol={startDateCol}
                    endDateCol={endDateCol}
                    extraColumns={extraColumns}
                    commit={commit}
                    onKeepRow={onKeepRow}
                    onRemoveRow={onRemoveRow}
                    flush={flush}
                    cellTarget={cellTarget}
                    onDone={() => setFocusedRowKey(null)}
                  />
                ) : (
                  <ServiceSummaryRow
                    key={row.key}
                    fieldKey={field.fieldKey}
                    row={row}
                    index={index}
                    disabled={disabled}
                    editorTeam={editorTeam}
                    serviceTypeCol={serviceTypeCol}
                    providerRoleCol={providerRoleCol}
                    frequencyCol={frequencyCol}
                    durationCol={durationCol}
                    locationCol={locationCol}
                    startDateCol={startDateCol}
                    endDateCol={endDateCol}
                    onKeepRow={onKeepRow}
                    onEdit={() => setFocusedRowKey(row.key)}
                  />
                )
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ServiceSummaryRow({
  fieldKey,
  row,
  index,
  disabled,
  editorTeam,
  serviceTypeCol,
  providerRoleCol,
  frequencyCol,
  durationCol,
  locationCol,
  startDateCol,
  endDateCol,
  onKeepRow,
  onEdit,
}: {
  fieldKey: string;
  row: KeyedRow;
  index: number;
  disabled?: boolean;
  editorTeam: StudentTeamCache | undefined;
  serviceTypeCol: TableColumn | undefined;
  providerRoleCol: TableColumn | undefined;
  frequencyCol: TableColumn | undefined;
  durationCol: TableColumn | undefined;
  locationCol: TableColumn | undefined;
  startDateCol: TableColumn | undefined;
  endDateCol: TableColumn | undefined;
  onKeepRow: (rowKey: string) => void;
  onEdit: () => void;
}) {
  const serviceTypeLabel = columnDisplayLabel(row, serviceTypeCol, 'Untitled service');
  const providerRoleLabel = columnDisplayLabel(row, providerRoleCol, '');
  const scheduleText = formatScheduleSummary(
    frequencyCol ? asText(row.cells[frequencyCol.columnKey]) : undefined,
    durationCol ? asText(row.cells[durationCol.columnKey]) : undefined
  );
  const settingLabel = columnDisplayLabel(row, locationCol, '');
  const dateRange = formatDateRange(
    startDateCol ? asText(row.cells[startDateCol.columnKey]) : undefined,
    endDateCol ? asText(row.cells[endDateCol.columnKey]) : undefined
  );
  const carried = carriedFrom(row);
  const reviewed = row.cells[ROW_CONFIRMED_KEY] === true;
  const owner = resolveOwnerDisplay(ownerUserId(row), editorTeam);

  return (
    <tr className="border-b border-brand-slate-100 last:border-0" data-testid={`field-${fieldKey}-row-${index}`}>
      <td className="px-3 py-2 align-top">
        <div className="font-medium text-brand-slate-800">{serviceTypeLabel}</div>
        {providerRoleLabel && <div className="text-xs text-brand-slate-500">{providerRoleLabel}</div>}
        {carried && (
          <div
            className={cn(
              'mt-1 inline-block rounded-badge border px-2 py-0.5 text-[11px]',
              reviewed ? 'border-brand-slate-200 text-brand-slate-500' : 'border-brand-amber-200 bg-brand-amber-50 text-brand-amber-700'
            )}
            data-testid={`field-${fieldKey}-row-${index}-carried`}
          >
            Carried from {carried.label ?? 'prior version'}
            {carried.date ? ` (${formatCarriedDate(carried.date)})` : ''}
            {!reviewed && ' · not yet reviewed'}
          </div>
        )}
      </td>
      <td className="px-3 py-2 align-top text-brand-slate-700">
        {scheduleText || <span className="italic text-brand-slate-500">Not set</span>}
      </td>
      <td className="px-3 py-2 align-top text-brand-slate-700">{settingLabel || '—'}</td>
      <td className="px-3 py-2 align-top text-brand-slate-700">{dateRange}</td>
      <td className="px-3 py-2 align-top">
        {owner ? (
          <span className={cn('text-brand-slate-700', owner.former && 'italic')}>{owner.label}</span>
        ) : (
          <span className="text-xs text-brand-amber-600">No owner</span>
        )}
      </td>
      <td className="px-3 py-2 text-right align-top">
        <div className="flex justify-end gap-2">
          {carried && !reviewed && !disabled && (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => onKeepRow(row.key)}
              data-testid={`field-${fieldKey}-row-${index}-keep`}
            >
              Keep as-is
            </Button>
          )}
          <Button
            variant="secondary"
            size="sm"
            id={serviceEditButtonDomId(fieldKey, row.key)}
            onClick={onEdit}
            data-testid={`field-${fieldKey}-row-${index}-edit`}
          >
            Edit
          </Button>
        </div>
      </td>
    </tr>
  );
}

function ServiceEditorRow({
  field,
  row,
  index,
  disabled,
  atMin,
  editorTeam,
  ownerWarning,
  saveStatus,
  serviceTypeCol,
  providerRoleCol,
  frequencyCol,
  durationCol,
  locationCol,
  startDateCol,
  endDateCol,
  extraColumns,
  commit,
  onKeepRow,
  onRemoveRow,
  flush,
  cellTarget,
  onDone,
}: {
  field: TemplateFieldDto;
  row: KeyedRow;
  index: number;
  disabled?: boolean;
  atMin: boolean;
  editorTeam: StudentTeamCache | undefined;
  ownerWarning?: string;
  saveStatus: AutosaveStatus;
  serviceTypeCol: TableColumn | undefined;
  providerRoleCol: TableColumn | undefined;
  frequencyCol: TableColumn | undefined;
  durationCol: TableColumn | undefined;
  locationCol: TableColumn | undefined;
  startDateCol: TableColumn | undefined;
  endDateCol: TableColumn | undefined;
  extraColumns: TableColumn[];
  commit: (updater: (current: KeyedRow[]) => KeyedRow[], immediate: boolean) => void;
  onKeepRow: (rowKey: string) => void;
  onRemoveRow: (rowKey: string) => void;
  flush: () => Promise<void>;
  cellTarget: CellTarget;
  onDone: () => void;
}) {
  const editorCtx = useDocumentEditorContext();
  const bodyRef = useRef<HTMLDivElement>(null);
  const persistedId = rowId(row);
  const carried = carriedFrom(row);
  const reviewed = row.cells[ROW_CONFIRMED_KEY] === true;

  // Opening a service moves focus into it; Done (or focusing a different
  // service) hands focus back to this row's own "Edit" button — same pattern
  // as `GoalEditor`.
  useEffect(() => {
    const raf = requestAnimationFrame(() => {
      bodyRef.current?.querySelector<HTMLElement>('input,textarea,select,[contenteditable="true"]')?.focus();
    });
    return () => {
      cancelAnimationFrame(raf);
      document.getElementById(serviceEditButtonDomId(field.fieldKey, row.key))?.focus();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- runs once per mount/unmount of this specific service's editor
  }, []);

  const updateCell = (columnKey: string, cell: TableCellValue, immediate = false) =>
    commit(
      (current) =>
        current.map((r) =>
          r.key === row.key
            ? {
                ...r,
                cells: {
                  ...r.cells,
                  [columnKey]: cell,
                  ...(carriedFrom(r) && !r.cells[ROW_CONFIRMED_KEY] ? { [ROW_CONFIRMED_KEY]: true } : {}),
                },
              }
            : r
        ),
      immediate
    );

  const updateOwner = (userId: number | undefined) =>
    commit((current) => current.map((r) => (r.key === row.key ? { ...r, cells: withOwner(r.cells, userId) } : r)), true);

  const renderColumn = (col: TableColumn, multiline = false) => {
    const cellId = `field-${field.fieldKey}-cell-${index}-${col.columnKey}`;
    return (
      <div key={col.columnKey} className={multiline ? 'md:col-span-3' : undefined}>
        <label htmlFor={cellId} className="mb-1 block text-sm font-medium text-brand-slate-700">
          {col.label || 'Field'}
          {col.required && (
            <span className="ml-1 text-brand-danger-700" aria-hidden="true">
              *
            </span>
          )}
        </label>
        <TableCell
          column={col}
          rowIndex={index}
          fieldKey={field.fieldKey}
          value={row.cells[col.columnKey]}
          disabled={disabled}
          multiline={multiline}
          inputId={cellId}
          onFocus={col.type === 'Text' ? () => editorCtx?.setActiveField(cellTarget(row.key, col)) : undefined}
          onChange={(cell) => updateCell(col.columnKey, cell)}
          onBlur={() => void flush()}
        />
      </div>
    );
  };

  const serviceLabel = serviceTypeCol ? row.cells[serviceTypeCol.columnKey] : undefined;
  const heading = typeof serviceLabel === 'string' && serviceLabel.trim() ? serviceLabel : `Service ${index + 1}`;
  const rowKinds: AssistKind[] = ['Rewrite', 'Improve'];

  return (
    <tr className="bg-brand-teal-50/40" data-testid={`field-${field.fieldKey}-row-${index}`}>
      <td colSpan={6} className="p-0">
        <div
          className="m-2 rounded-card border-2 border-brand-teal-400 bg-white p-4 shadow-sm"
          aria-label={`Editing service ${index + 1}`}
        >
          <div className="flex flex-wrap items-center gap-3 border-b border-brand-slate-100 pb-3">
            <h3 className="font-serif text-lg text-brand-slate-800">Editing: {heading}</h3>
            <AutosaveIndicator status={saveStatus} />
            {carried && !reviewed && !disabled && (
              <Button
                variant="secondary"
                size="sm"
                onClick={() => onKeepRow(row.key)}
                data-testid={`field-${field.fieldKey}-row-${index}-keep`}
              >
                Keep as-is
              </Button>
            )}
            <div className="ml-auto flex items-center gap-3 text-sm">
              <button
                type="button"
                className="text-brand-danger-700 hover:underline disabled:opacity-50"
                disabled={disabled || atMin}
                onClick={() => onRemoveRow(row.key)}
                data-testid={`field-${field.fieldKey}-remove-${index}`}
              >
                Remove
              </button>
              <Button size="sm" onClick={onDone} data-testid={`field-${field.fieldKey}-row-${index}-done`}>
                Done
              </Button>
            </div>
          </div>

          <div ref={bodyRef} className="mt-4 grid gap-4 md:grid-cols-3">
            {serviceTypeCol && renderColumn(serviceTypeCol)}
            {providerRoleCol && renderColumn(providerRoleCol)}
            <div>
              <label className="mb-1 block text-sm font-medium text-brand-slate-700" htmlFor={`field-${field.fieldKey}-owner-${index}`}>
                Owner
              </label>
              <TeamMemberSelect
                id={`field-${field.fieldKey}-owner-${index}`}
                team={editorTeam}
                value={ownerUserId(row)}
                disabled={disabled}
                onChange={updateOwner}
                warning={ownerWarning}
                data-testid={`field-${field.fieldKey}-row-${index}-owner`}
              />
            </div>

            {frequencyCol && (
              <div>
                <label className="mb-1 block text-sm font-medium text-brand-slate-700" htmlFor={`field-${field.fieldKey}-cell-${index}-${frequencyCol.columnKey}`}>
                  Frequency
                </label>
                <FrequencyField
                  id={`field-${field.fieldKey}-cell-${index}-${frequencyCol.columnKey}`}
                  value={asText(row.cells[frequencyCol.columnKey])}
                  disabled={disabled}
                  onChange={(text) => updateCell(frequencyCol.columnKey, text)}
                  onImmediateChange={(text) => updateCell(frequencyCol.columnKey, text, true)}
                  onBlur={() => void flush()}
                  testId={`field-${field.fieldKey}-cell-${index}-${frequencyCol.columnKey}`}
                />
              </div>
            )}

            {durationCol && (
              <div>
                <label className="mb-1 block text-sm font-medium text-brand-slate-700" htmlFor={`field-${field.fieldKey}-cell-${index}-${durationCol.columnKey}`}>
                  Minutes per session
                </label>
                <DurationField
                  id={`field-${field.fieldKey}-cell-${index}-${durationCol.columnKey}`}
                  value={asText(row.cells[durationCol.columnKey])}
                  disabled={disabled}
                  onChange={(text) => updateCell(durationCol.columnKey, text)}
                  onImmediateChange={(text) => updateCell(durationCol.columnKey, text, true)}
                  onBlur={() => void flush()}
                  testId={`field-${field.fieldKey}-cell-${index}-${durationCol.columnKey}`}
                />
              </div>
            )}

            {locationCol && (
              <div>
                <label className="mb-1 block text-sm font-medium text-brand-slate-700" htmlFor={`field-${field.fieldKey}-cell-${index}-${locationCol.columnKey}`}>
                  Setting
                </label>
                <SettingField
                  id={`field-${field.fieldKey}-cell-${index}-${locationCol.columnKey}`}
                  column={locationCol}
                  rowIndex={index}
                  fieldKey={field.fieldKey}
                  value={row.cells[locationCol.columnKey]}
                  disabled={disabled}
                  onChange={(cell) => updateCell(locationCol.columnKey, cell, locationCol.type === 'Select')}
                  onBlur={() => void flush()}
                />
              </div>
            )}

            {startDateCol && renderColumn(startDateCol)}
            {endDateCol && renderColumn(endDateCol)}
            {extraColumns.map((col) => renderColumn(col, col.type === 'Text'))}
          </div>

          {serviceTypeCol && persistedId ? (
            <FieldAssistBar
              fieldKey={field.fieldKey}
              rowId={persistedId}
              kinds={rowKinds}
              allowPull={false}
              onApply={(text) => {
                updateCell(serviceTypeCol.columnKey, text);
                void flush();
              }}
              beforeRequest={flush}
              disabled={disabled}
              testIdPrefix={`field-${field.fieldKey}-row-${index}`}
            />
          ) : (
            !disabled && <p className="mt-2 text-xs text-brand-slate-500">AI help is available once this service has saved.</p>
          )}
        </div>
      </td>
    </tr>
  );
}

/**
 * Frequency's structured count + period controls, normalizing to "N per
 * week"/"N per month"/"N per day" text on every change. Falls back to a
 * plain text input — with a one-click switch to structured entry — when the
 * value in the column is text the parser doesn't recognize, so an existing
 * district's free-form phrasing is never silently destroyed.
 */
function FrequencyField({
  id,
  value,
  disabled,
  onChange,
  onImmediateChange,
  onBlur,
  testId,
}: {
  id: string;
  value: string;
  disabled?: boolean;
  onChange: (text: string) => void;
  onImmediateChange: (text: string) => void;
  onBlur: () => void;
  testId: string;
}) {
  const initialParsed = parseFrequency(value);
  const [mode, setMode] = useState<'structured' | 'free'>(() => (value.trim() === '' || initialParsed ? 'structured' : 'free'));
  const [count, setCount] = useState(() => String(initialParsed?.count ?? 1));
  const [period, setPeriod] = useState<ServicePeriod>(() => initialParsed?.period ?? 'week');

  if (mode === 'free') {
    return (
      <div>
        <input
          id={id}
          type="text"
          value={value}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cellInputClass}
          data-testid={testId}
        />
        {!disabled && (
          <button
            type="button"
            className="mt-1 text-xs text-brand-teal-600 hover:underline"
            onClick={() => {
              const next = parseFrequency(value) ?? { count: 1, period: 'week' as ServicePeriod };
              setCount(String(next.count));
              setPeriod(next.period);
              setMode('structured');
              onImmediateChange(formatFrequencyText(next));
            }}
            data-testid={`${testId}-switch-structured`}
          >
            Switch to structured entry
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <input
        id={id}
        type="number"
        min={1}
        value={count}
        disabled={disabled}
        onChange={(e) => {
          setCount(e.target.value);
          const n = Number(e.target.value);
          if (Number.isFinite(n) && n > 0) onChange(formatFrequencyText({ count: n, period }));
        }}
        onBlur={onBlur}
        className={cn(cellInputClass, 'w-20')}
        data-testid={testId}
      />
      <span className="text-brand-slate-500" aria-hidden="true">
        ×
      </span>
      <select
        aria-label="Frequency period"
        value={period}
        disabled={disabled}
        onChange={(e) => {
          const p = e.target.value as ServicePeriod;
          setPeriod(p);
          const n = Number(count);
          if (Number.isFinite(n) && n > 0) onImmediateChange(formatFrequencyText({ count: n, period: p }));
        }}
        className={cn(cellInputClass, 'flex-1')}
        data-testid={`${testId}-period`}
      >
        <option value="week">per week</option>
        <option value="month">per month</option>
        <option value="day">per day</option>
      </select>
    </div>
  );
}

/** Duration's structured minutes-per-session control, normalizing to "M
 *  minutes" text on every change, with the same free-text fallback as
 *  frequency. */
function DurationField({
  id,
  value,
  disabled,
  onChange,
  onImmediateChange,
  onBlur,
  testId,
}: {
  id: string;
  value: string;
  disabled?: boolean;
  onChange: (text: string) => void;
  onImmediateChange: (text: string) => void;
  onBlur: () => void;
  testId: string;
}) {
  const initialParsed = parseDurationMinutes(value);
  const [mode, setMode] = useState<'structured' | 'free'>(() => (value.trim() === '' || initialParsed != null ? 'structured' : 'free'));
  const [minutes, setMinutes] = useState(() => (initialParsed != null ? String(initialParsed) : ''));

  if (mode === 'free') {
    return (
      <div>
        <input
          id={id}
          type="text"
          value={value}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cellInputClass}
          data-testid={testId}
        />
        {!disabled && (
          <button
            type="button"
            className="mt-1 text-xs text-brand-teal-600 hover:underline"
            onClick={() => {
              const n = parseDurationMinutes(value) ?? 30;
              setMinutes(String(n));
              setMode('structured');
              onImmediateChange(formatDurationText(n));
            }}
            data-testid={`${testId}-switch-structured`}
          >
            Switch to structured entry
          </button>
        )}
      </div>
    );
  }

  return (
    <input
      id={id}
      type="number"
      min={1}
      value={minutes}
      disabled={disabled}
      onChange={(e) => {
        setMinutes(e.target.value);
        const n = Number(e.target.value);
        if (Number.isFinite(n) && n > 0) onChange(formatDurationText(n));
      }}
      onBlur={onBlur}
      className={cellInputClass}
      data-testid={testId}
    />
  );
}

const SETTING_SUGGESTIONS = [
  'General education classroom',
  'Special education setting',
  'Related services room',
  'Home',
];

/** Setting/location: the template's own Select options when the column is a
 *  Select, else a free-text input offering the common settings via a
 *  `<datalist>` (plan 2026-10-02-002, Phase 4). */
function SettingField({
  id,
  column,
  rowIndex,
  fieldKey,
  value,
  disabled,
  onChange,
  onBlur,
}: {
  id: string;
  column: TableColumn;
  rowIndex: number;
  fieldKey: string;
  value: TableCellValue | undefined;
  disabled?: boolean;
  onChange: (cell: TableCellValue) => void;
  onBlur: () => void;
}) {
  if (column.type === 'Select') {
    return (
      <TableCell
        column={column}
        rowIndex={rowIndex}
        fieldKey={fieldKey}
        value={value}
        disabled={disabled}
        inputId={id}
        onChange={onChange}
        onBlur={onBlur}
      />
    );
  }

  const strValue = typeof value === 'string' ? value : '';
  const listId = `${id}-options`;
  return (
    <>
      <input
        id={id}
        type="text"
        list={listId}
        value={strValue}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        onBlur={onBlur}
        className={cellInputClass}
        data-testid={`field-${fieldKey}-cell-${rowIndex}-${column.columnKey}`}
      />
      <datalist id={listId}>
        {SETTING_SUGGESTIONS.map((s) => (
          <option key={s} value={s} />
        ))}
      </datalist>
    </>
  );
}
