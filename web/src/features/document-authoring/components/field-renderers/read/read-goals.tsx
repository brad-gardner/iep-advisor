import { Ruler, CalendarClock, ListChecks } from 'lucide-react';
import { parseConfig } from '@/features/admin/templates/template-config';
import { ROW_CONFIRMED_KEY, ROW_OBJECTIVES_KEY } from '@/features/admin/templates/document-semantics';
import { Markdown } from '@/components/ui/markdown';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/cn';
import { useDocumentEditorContext } from '../../../hooks/document-editor-context';
import { carriedFrom, coerceRows, ownerUserId, rowId, type KeyedRow } from '../../../lib/table-rows';
import { columnDisplayLabel } from '../../../lib/group-rows';
import { resolveOwnerDisplay } from '../../../lib/owner-display';
import { formatCarriedDate } from '../../../lib/table-cell-format';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

/**
 * Read view for the goals block (plan 2026-10-02-002, Phase 3): one compact
 * card per goal — number, area, a clamped goal statement, measurement/
 * timeframe/objectives-count chips, owner, and carried-forward/reviewed
 * badges — with an "Edit goal" button per card. Clicking it calls `onEditRow`
 * with the goal's persisted `_rowId` (every row here has one: read mode only
 * ever shows saved values) so `SectionCard` can open the section AND land
 * directly in that goal's focused editor, instead of the generic Edit.
 */
export function ReadGoals({ field, value, onEditRow }: ReadFieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const domainCol = columns.find((c) => c.semantic === 'domain');
  const goalTextCol = columns.find((c) => c.semantic === 'goalText');
  const measurementCol = columns.find((c) => c.semantic === 'measurementMethod');
  const timeframeCol = columns.find((c) => c.semantic === 'timeframe');
  const rows = coerceRows(value);
  const editor = useDocumentEditorContext();

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <div className="flex items-baseline gap-2">
        <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
        <span className="text-xs text-brand-slate-500">
          {rows.length} goal{rows.length === 1 ? '' : 's'}
        </span>
      </div>
      {rows.length === 0 ? (
        <p className="mt-0.5 text-[15px] italic text-brand-slate-500">No goals yet.</p>
      ) : (
        <ol className="mt-2 space-y-3">
          {rows.map((row, index) => (
            <GoalReadCard
              key={row.key}
              row={row}
              index={index}
              domainLabel={domainCol ? columnDisplayLabel(row, domainCol, '') : ''}
              goalText={goalTextCol ? row.cells[goalTextCol.columnKey] : undefined}
              measurement={measurementCol ? row.cells[measurementCol.columnKey] : undefined}
              timeframe={timeframeCol ? row.cells[timeframeCol.columnKey] : undefined}
              owner={resolveOwnerDisplay(ownerUserId(row), editor?.team)}
              onEdit={onEditRow ? () => onEditRow(rowId(row) ?? row.key) : undefined}
            />
          ))}
        </ol>
      )}
    </div>
  );
}

function GoalReadCard({
  row,
  index,
  domainLabel,
  goalText,
  measurement,
  timeframe,
  owner,
  onEdit,
}: {
  row: KeyedRow;
  index: number;
  domainLabel: string;
  goalText: unknown;
  measurement: unknown;
  timeframe: unknown;
  owner: ReturnType<typeof resolveOwnerDisplay>;
  onEdit?: () => void;
}) {
  const carried = carriedFrom(row);
  const reviewed = row.cells[ROW_CONFIRMED_KEY] === true;
  const objectives = row.cells[ROW_OBJECTIVES_KEY];
  const objectivesCount = Array.isArray(objectives) ? objectives.length : 0;
  const text = typeof goalText === 'string' ? goalText : '';
  const measurementText = typeof measurement === 'string' ? measurement.trim() : '';
  const timeframeText = typeof timeframe === 'string' ? timeframe.trim() : '';

  return (
    <li
      className="rounded-card border border-brand-slate-200 p-4"
      data-testid={`read-goal-${index}`}
    >
      <div className="flex items-start gap-4">
        <div
          className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-teal-50 text-sm font-semibold text-brand-teal-700"
          aria-hidden="true"
        >
          {index + 1}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2 text-xs">
            {domainLabel && (
              <span className="rounded-badge bg-brand-slate-100 px-2 py-0.5 text-brand-slate-700">{domainLabel}</span>
            )}
            {carried && (
              <span
                className={cn(
                  'rounded-badge border px-2 py-0.5',
                  reviewed ? 'border-brand-slate-200 text-brand-slate-500' : 'border-brand-amber-200 bg-brand-amber-50 text-brand-amber-700'
                )}
                data-testid={`read-goal-${index}-carried`}
              >
                Carried from {carried.label ?? 'prior version'}
                {carried.date ? ` (${formatCarriedDate(carried.date)})` : ''}
                {!reviewed && ' · needs review'}
              </span>
            )}
          </div>
          {text.trim() ? (
            <div className="mt-2 line-clamp-3 text-[15px] leading-relaxed text-brand-slate-800">
              <Markdown content={text} disableLinks />
            </div>
          ) : (
            <p className="mt-2 text-[15px] italic text-brand-slate-500">No goal statement yet</p>
          )}
          <div className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-1 text-sm text-brand-slate-600">
            {measurementText && (
              <span className="inline-flex items-center gap-1.5">
                <Ruler className="h-3.5 w-3.5 text-brand-slate-400" aria-hidden="true" />
                {measurementText}
              </span>
            )}
            {timeframeText && (
              <span className="inline-flex items-center gap-1.5">
                <CalendarClock className="h-3.5 w-3.5 text-brand-slate-400" aria-hidden="true" />
                {timeframeText}
              </span>
            )}
            <span className="inline-flex items-center gap-1.5">
              <ListChecks className="h-3.5 w-3.5 text-brand-slate-400" aria-hidden="true" />
              {objectivesCount} objective{objectivesCount === 1 ? '' : 's'}
            </span>
          </div>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-2 text-right">
          {owner ? (
            <span className={cn('text-sm text-brand-slate-600', owner.former && 'italic')}>{owner.label}</span>
          ) : (
            <span className="text-xs text-brand-amber-600">No owner</span>
          )}
          {onEdit && (
            <Button variant="secondary" size="sm" onClick={onEdit} data-testid={`read-goal-${index}-edit`}>
              Edit goal
            </Button>
          )}
        </div>
      </div>
    </li>
  );
}
