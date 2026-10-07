import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus } from 'lucide-react';
import type { TemplateFieldDto } from '@/features/admin/templates/types';
import type { TableColumn } from '@/features/admin/templates/template-config';
import { ROW_CONFIRMED_KEY, ROW_OBJECTIVES_KEY } from '@/features/admin/templates/document-semantics';
import { Button } from '@/components/ui/button';
import type { AutosaveStatus } from '@/hooks/use-autosave';
import { AutosaveIndicator } from '@/features/admin/templates/components/autosave-indicator';
import type { AssistKind } from '../../api/assist-types';
import type { StudentTeamCache } from '../../hooks/use-student-team';
import { useDocumentEditorContext } from '../../hooks/document-editor-context';
import { carriedFrom, emptyCells, nextRowKey, ownerUserId, rowId, withOwner, type KeyedRow } from '../../lib/table-rows';
import type { KeyedObjective } from '../../lib/objective-rows';
import type { TableCellValue } from '../../types';
import { resolveOwnerDisplay } from '../../lib/owner-display';
import { columnDisplayLabel } from '../../lib/group-rows';
import { isLongColumn, isRichTextColumn } from '../../lib/table-cell-format';
import { TableCell } from './table-field';
import { FieldAssistBar } from './field-assist-bar';
import { ObjectivesEditor } from './objectives-editor';
import { GoalReadCard } from './read/read-goals';
import { TeamMemberSelect } from '../team-member-select';
import { fieldElementId } from './types';

type CellTarget = (rowKey: string, col: TableColumn) => { id: string; label: () => string; apply: (text: string) => void };

interface GoalsBlockProps {
  field: TemplateFieldDto;
  columns: TableColumn[];
  rows: KeyedRow[];
  disabled?: boolean;
  atMax: boolean;
  editorTeam: StudentTeamCache | undefined;
  ownerWarnings: Record<string, string>;
  /** Lands directly in this goal's focused editor (a read-card's "Edit goal"),
   *  instead of defaulting to the compact card list. */
  initialFocusRowKey?: string;
  saveStatus: AutosaveStatus;
  commit: (updater: (current: KeyedRow[]) => KeyedRow[], immediate: boolean) => void;
  onKeepRow: (rowKey: string) => void;
  onRequestRemove: (rowKey: string, label: string) => void;
  flush: () => Promise<void>;
  cellTarget: CellTarget;
}

function goalEditButtonDomId(fieldKey: string, rowKey: string): string {
  return `field-${fieldKey}-goal-${rowKey}-edit-button`;
}

/**
 * Goals block (plan 2026-10-02-002, Phase 3): a compact card per goal, one of
 * which may be swapped for the full `GoalEditor` — "+ Add goal" always
 * appends (never inserts), which keeps a brand-new, still-blank objective or
 * goal from ever landing anywhere but the end of its list (see
 * `objective-rows.ts` on why that matters for id adoption).
 */
export function GoalsBlock({
  field,
  columns,
  rows,
  disabled,
  atMax,
  editorTeam,
  ownerWarnings,
  initialFocusRowKey,
  saveStatus,
  commit,
  onKeepRow,
  onRequestRemove,
  flush,
  cellTarget,
}: GoalsBlockProps) {
  const { t } = useTranslation('document-authoring');
  const labelId = `${fieldElementId(field.id)}-label`;
  const [focusedRowKey, setFocusedRowKey] = useState<string | null>(() => initialFocusRowKey ?? null);

  // Shared with the summary card below (GoalReadCard) — same lookups
  // GoalEditor makes for its own fields.
  const domainCol = columns.find((c) => c.semantic === 'domain');
  const goalTextCol = columns.find((c) => c.semantic === 'goalText');
  const measurementCol = columns.find((c) => c.semantic === 'measurementMethod');
  const timeframeCol = columns.find((c) => c.semantic === 'timeframe');

  const addGoal = () => {
    const key = nextRowKey();
    commit((current) => [...current, { key, cells: emptyCells(columns) }], true);
    setFocusedRowKey(key);
  };

  return (
    <div id={fieldElementId(field.id)} tabIndex={-1} role="group" aria-labelledby={labelId} data-testid={`field-${field.fieldKey}`}>
      <div className="mb-3 flex flex-wrap items-center gap-3">
        <h3 id={labelId} className="text-[13px] font-medium text-brand-slate-600">
          {field.label || t('goalsBlock.fallbackHeading')}
          {field.required && (
            <span className="ml-1 text-brand-danger-700" aria-hidden="true">
              *
            </span>
          )}
        </h3>
        <span className="text-xs text-brand-slate-500">{t('goalsBlock.goalCount', { count: rows.length })}</span>
        <Button
          variant="secondary"
          size="sm"
          className="ml-auto"
          disabled={disabled || atMax}
          onClick={addGoal}
          data-testid={`field-${field.fieldKey}-add`}
        >
          <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
          {t('goalsBlock.addGoal')}
        </Button>
      </div>

      {rows.length === 0 ? (
        <p className="text-sm text-brand-slate-500">{t('goalsBlock.noGoalsYet')}</p>
      ) : (
        <ol className="space-y-3">
          {rows.map((row, index) => {
            const persistedId = rowId(row);
            return row.key === focusedRowKey ? (
              <GoalEditor
                key={row.key}
                field={field}
                columns={columns}
                row={row}
                index={index}
                disabled={disabled}
                editorTeam={editorTeam}
                ownerWarning={persistedId ? ownerWarnings[persistedId] : undefined}
                saveStatus={saveStatus}
                commit={commit}
                onKeepRow={onKeepRow}
                onRequestRemove={onRequestRemove}
                flush={flush}
                cellTarget={cellTarget}
                onDone={() => setFocusedRowKey(null)}
              />
            ) : (
              <GoalReadCard
                key={row.key}
                row={row}
                index={index}
                domainLabel={domainCol ? columnDisplayLabel(row, domainCol, t('goalsBlock.noAreaSet')) : ''}
                goalText={goalTextCol ? row.cells[goalTextCol.columnKey] : undefined}
                measurement={measurementCol ? row.cells[measurementCol.columnKey] : undefined}
                timeframe={timeframeCol ? row.cells[timeframeCol.columnKey] : undefined}
                owner={resolveOwnerDisplay(ownerUserId(row), editorTeam)}
                onEdit={() => setFocusedRowKey(row.key)}
                editButtonId={goalEditButtonDomId(field.fieldKey, row.key)}
                testIdPrefix={`field-${field.fieldKey}-row-${index}`}
              />
            );
          })}
        </ol>
      )}
    </div>
  );
}

function GoalEditor({
  field,
  columns,
  row,
  index,
  disabled,
  editorTeam,
  ownerWarning,
  saveStatus,
  commit,
  onKeepRow,
  onRequestRemove,
  flush,
  cellTarget,
  onDone,
}: {
  field: TemplateFieldDto;
  columns: TableColumn[];
  row: KeyedRow;
  index: number;
  disabled?: boolean;
  editorTeam: StudentTeamCache | undefined;
  ownerWarning?: string;
  saveStatus: AutosaveStatus;
  commit: (updater: (current: KeyedRow[]) => KeyedRow[], immediate: boolean) => void;
  onKeepRow: (rowKey: string) => void;
  onRequestRemove: (rowKey: string, label: string) => void;
  flush: () => Promise<void>;
  cellTarget: CellTarget;
  onDone: () => void;
}) {
  const { t } = useTranslation('document-authoring');
  const editorCtx = useDocumentEditorContext();
  const bodyRef = useRef<HTMLDivElement>(null);
  const persistedId = rowId(row);
  const carried = carriedFrom(row);
  const reviewed = row.cells[ROW_CONFIRMED_KEY] === true;

  const domainCol = columns.find((c) => c.semantic === 'domain');
  const timeframeCol = columns.find((c) => c.semantic === 'timeframe');
  const goalTextCol = columns.find((c) => c.semantic === 'goalText');
  const baselineCol = columns.find((c) => c.semantic === 'baseline');
  const targetCriteriaCol = columns.find((c) => c.semantic === 'targetCriteria');
  const measurementCol = columns.find((c) => c.semantic === 'measurementMethod');
  const known = new Set(
    [domainCol, timeframeCol, goalTextCol, baselineCol, targetCriteriaCol, measurementCol]
      .filter((c): c is TableColumn => c != null)
      .map((c) => c.columnKey)
  );
  // Any column a district added to its Goals table beyond the known
  // semantics (e.g. a progress-report-schedule column) still renders — just
  // generically, below the recognized fields.
  const extraColumns = columns.filter((c) => !known.has(c.columnKey));

  // Opening a goal moves focus into it; Done (or focusing a different goal)
  // hands focus back to this card's own "Edit goal" button — same pattern as
  // SectionCard's Edit/Done focus management, scoped to one goal.
  useEffect(() => {
    const raf = requestAnimationFrame(() => {
      bodyRef.current?.querySelector<HTMLElement>('input,textarea,select,[contenteditable="true"]')?.focus();
    });
    return () => {
      cancelAnimationFrame(raf);
      document.getElementById(goalEditButtonDomId(field.fieldKey, row.key))?.focus();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- runs once per mount/unmount of this specific goal's editor
  }, []);

  const updateCell = (columnKey: string, cell: TableCellValue) =>
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
      false
    );

  const updateOwner = (userId: number | undefined) =>
    commit((current) => current.map((r) => (r.key === row.key ? { ...r, cells: withOwner(r.cells, userId) } : r)), true);

  const updateObjectives = (objectives: KeyedObjective[], immediate: boolean) =>
    commit(
      (current) =>
        current.map((r) =>
          r.key === row.key
            ? {
                ...r,
                cells: {
                  ...r.cells,
                  [ROW_OBJECTIVES_KEY]: objectives,
                  ...(carriedFrom(r) && !r.cells[ROW_CONFIRMED_KEY] ? { [ROW_CONFIRMED_KEY]: true } : {}),
                },
              }
            : r
        ),
      immediate
    );

  const renderColumn = (col: TableColumn) => {
    const cellId = `field-${field.fieldKey}-cell-${index}-${col.columnKey}`;
    return (
      <div key={col.columnKey}>
        <label htmlFor={cellId} className="mb-1 block text-sm font-medium text-brand-slate-700">
          {col.label || t('goalsBlock.fieldFallback')}
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
          richText={isRichTextColumn(col.semantic)}
          multiline={isLongColumn(col.semantic)}
          inputId={cellId}
          onFocus={col.type === 'Text' ? () => editorCtx?.setActiveField(cellTarget(row.key, col)) : undefined}
          onChange={(cell) => updateCell(col.columnKey, cell)}
          onBlur={() => void flush()}
        />
      </div>
    );
  };

  const rowKinds: AssistKind[] = ['Rewrite', 'Improve', 'SuggestMeasurement'];

  return (
    <li
      className="rounded-card border-2 border-brand-teal-400 bg-white shadow-sm"
      aria-label={`Editing goal ${index + 1}`}
      data-testid={`field-${field.fieldKey}-row-${index}`}
    >
      <div className="flex flex-wrap items-center gap-3 border-b border-brand-slate-100 px-5 py-3">
        <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-teal-500 text-sm font-semibold text-white" aria-hidden="true">
          {index + 1}
        </div>
        <h3 className="font-serif text-lg text-brand-slate-800">{t('goalsBlock.editingGoal', { number: index + 1 })}</h3>
        <AutosaveIndicator status={saveStatus} />
        {carried && !reviewed && !disabled && (
          <Button
            variant="secondary"
            size="sm"
            onClick={() => onKeepRow(row.key)}
            data-testid={`field-${field.fieldKey}-row-${index}-keep`}
          >
            {t('goalsBlock.keepAsIs')}
          </Button>
        )}
        <div className="ml-auto flex items-center gap-3 text-sm">
          <button
            type="button"
            className="text-brand-danger-700 hover:underline disabled:opacity-50"
            disabled={disabled}
            onClick={() => {
              const label = goalTextCol ? row.cells[goalTextCol.columnKey] : undefined;
              onRequestRemove(row.key, typeof label === 'string' ? label : '');
            }}
            data-testid={`field-${field.fieldKey}-remove-${index}`}
          >
            {t('goalsBlock.removeGoal')}
          </button>
          <Button size="sm" onClick={onDone} data-testid={`field-${field.fieldKey}-row-${index}-done`}>
            {t('goalsBlock.done')}
          </Button>
        </div>
      </div>

      <div ref={bodyRef} className="grid gap-6 p-5 xl:grid-cols-[minmax(0,1fr)_18rem]">
        <div className="min-w-0 space-y-5">
          <div className="grid gap-4 sm:grid-cols-2">
            {domainCol && renderColumn(domainCol)}
            {timeframeCol && renderColumn(timeframeCol)}
          </div>

          {goalTextCol && (
            <div>
              {renderColumn(goalTextCol)}
              {persistedId ? (
                <FieldAssistBar
                  fieldKey={field.fieldKey}
                  rowId={persistedId}
                  kinds={rowKinds}
                  allowPull
                  onApply={(text) => {
                    updateCell(goalTextCol.columnKey, text);
                    void flush();
                  }}
                  beforeRequest={flush}
                  disabled={disabled}
                  testIdPrefix={`field-${field.fieldKey}-row-${index}`}
                />
              ) : (
                !disabled && <p className="mt-2 text-xs text-brand-slate-500">{t('goalsBlock.aiHelpAvailableAfterSave')}</p>
              )}
            </div>
          )}

          <div className="grid gap-4 md:grid-cols-2">
            {baselineCol && renderColumn(baselineCol)}
            {targetCriteriaCol && renderColumn(targetCriteriaCol)}
          </div>

          {(measurementCol || extraColumns.length > 0) && (
            <div className="grid gap-4 sm:grid-cols-2">
              {measurementCol && renderColumn(measurementCol)}
              {extraColumns.map((col) => renderColumn(col))}
            </div>
          )}

          <ObjectivesEditor
            value={row.cells[ROW_OBJECTIVES_KEY]}
            disabled={disabled}
            testIdPrefix={`field-${field.fieldKey}-row-${index}`}
            onChange={updateObjectives}
            flush={flush}
          />
        </div>

        <aside className="space-y-4">
          <div className="rounded-card border border-brand-slate-200 p-4">
            <label className="text-sm font-semibold text-brand-slate-700" htmlFor={`field-${field.fieldKey}-owner-${index}`}>
              {t('goalsBlock.owner')}
            </label>
            <p className="text-xs text-brand-slate-500">{t('goalsBlock.ownerHint')}</p>
            <div className="mt-2">
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
          </div>
          <div className="rounded-card border border-brand-slate-200 p-4 text-sm">
            <div className="font-semibold text-brand-slate-700">{t('goalsBlock.evidenceHeading')}</div>
            <p className="mt-1 text-xs text-brand-slate-500">{t('goalsBlock.evidenceHint')}</p>
          </div>
        </aside>
      </div>
    </li>
  );
}
