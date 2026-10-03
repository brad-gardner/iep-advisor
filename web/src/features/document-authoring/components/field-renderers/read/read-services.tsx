import { Button } from '@/components/ui/button';
import { parseConfig, type TableColumn } from '@/features/admin/templates/template-config';
import { ROW_CONFIRMED_KEY } from '@/features/admin/templates/document-semantics';
import { cn } from '@/lib/cn';
import { useDocumentEditorContext } from '../../../hooks/document-editor-context';
import { carriedFrom, coerceRows, ownerUserId, rowId, type KeyedRow } from '../../../lib/table-rows';
import { columnDisplayLabel } from '../../../lib/group-rows';
import { resolveOwnerDisplay, type OwnerDisplay } from '../../../lib/owner-display';
import { formatCarriedDate, formatDateRange } from '../../../lib/table-cell-format';
import { formatScheduleSummary, totalMinutesPerWeek } from '../../../lib/service-schedule';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

function asText(v: unknown): string | undefined {
  return typeof v === 'string' ? v : undefined;
}

/**
 * Read view for the services block (plan 2026-10-02-002, Phase 4): a schedule
 * table — service + provider role, frequency × minutes, setting, dates and
 * owner — with a header total of minutes/week and an "Edit" button per row
 * that calls `onEditRow` with the service's persisted `_rowId`, same pattern
 * as `ReadGoals`'s "Edit goal" (`SectionCard` opens the section and lands
 * directly in that row's inline editor).
 */
export function ReadServices({ field, value, onEditRow }: ReadFieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const serviceTypeCol = columns.find((c) => c.semantic === 'serviceType') ?? columns.find((c) => c.type === 'Text');
  const providerRoleCol = columns.find((c) => c.semantic === 'providerRole');
  const frequencyCol = columns.find((c) => c.semantic === 'frequency');
  const durationCol = columns.find((c) => c.semantic === 'duration');
  const locationCol = columns.find((c) => c.semantic === 'location');
  const startDateCol = columns.find((c) => c.semantic === 'startDate');
  const endDateCol = columns.find((c) => c.semantic === 'endDate');
  const rows = coerceRows(value);
  const editor = useDocumentEditorContext();

  const totals = totalMinutesPerWeek(
    rows.map((row) => ({
      frequencyText: frequencyCol ? asText(row.cells[frequencyCol.columnKey]) : undefined,
      durationText: durationCol ? asText(row.cells[durationCol.columnKey]) : undefined,
    }))
  );

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <div className="flex flex-wrap items-baseline gap-2">
        <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
        {rows.length > 0 && (
          <span className="text-xs text-brand-slate-500" data-testid={`read-field-${field.fieldKey}-total`}>
            {rows.length} service{rows.length === 1 ? '' : 's'} · {totals.totalMinutesPerWeek} min/week
            {totals.excludedCount > 0 &&
              ` (${totals.excludedCount} ${totals.excludedCount === 1 ? 'service' : 'services'} not counted — frequency/duration unclear)`}
          </span>
        )}
      </div>
      {rows.length === 0 ? (
        <p className="mt-0.5 text-[15px] italic text-brand-slate-500">No services yet.</p>
      ) : (
        <div className="mt-2 overflow-x-auto rounded-card border border-brand-slate-200">
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
                {onEditRow && (
                  <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                    <span className="sr-only">Actions</span>
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, index) => (
                <ServiceReadRow
                  key={row.key}
                  row={row}
                  index={index}
                  serviceTypeCol={serviceTypeCol}
                  providerRoleCol={providerRoleCol}
                  frequencyCol={frequencyCol}
                  durationCol={durationCol}
                  locationCol={locationCol}
                  startDateCol={startDateCol}
                  endDateCol={endDateCol}
                  owner={resolveOwnerDisplay(ownerUserId(row), editor?.team)}
                  onEdit={onEditRow ? () => onEditRow(rowId(row) ?? row.key) : undefined}
                />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function ServiceReadRow({
  row,
  index,
  serviceTypeCol,
  providerRoleCol,
  frequencyCol,
  durationCol,
  locationCol,
  startDateCol,
  endDateCol,
  owner,
  onEdit,
}: {
  row: KeyedRow;
  index: number;
  serviceTypeCol: TableColumn | undefined;
  providerRoleCol: TableColumn | undefined;
  frequencyCol: TableColumn | undefined;
  durationCol: TableColumn | undefined;
  locationCol: TableColumn | undefined;
  startDateCol: TableColumn | undefined;
  endDateCol: TableColumn | undefined;
  owner: OwnerDisplay | null;
  onEdit?: () => void;
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

  return (
    <tr className="border-b border-brand-slate-100 last:border-0" data-testid={`read-service-${index}`}>
      <td className="px-3 py-2 align-top">
        <div className="font-medium text-brand-slate-800">{serviceTypeLabel}</div>
        {providerRoleLabel && <div className="text-xs text-brand-slate-500">{providerRoleLabel}</div>}
        {carried && (
          <div
            className={cn(
              'mt-1 inline-block rounded-badge border px-2 py-0.5 text-[11px]',
              reviewed ? 'border-brand-slate-200 text-brand-slate-500' : 'border-brand-amber-200 bg-brand-amber-50 text-brand-amber-700'
            )}
            data-testid={`read-service-${index}-carried`}
          >
            Carried from {carried.label ?? 'prior version'}
            {carried.date ? ` (${formatCarriedDate(carried.date)})` : ''}
            {!reviewed && ' · needs review'}
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
      {onEdit && (
        <td className="px-3 py-2 text-right align-top">
          <Button variant="secondary" size="sm" onClick={onEdit} data-testid={`read-service-${index}-edit`}>
            Edit
          </Button>
        </td>
      )}
    </tr>
  );
}
