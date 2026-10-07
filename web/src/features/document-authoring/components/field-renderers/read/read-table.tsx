import { useTranslation } from 'react-i18next';
import i18n from '@/lib/i18n';
import { parseConfig, readColumnOptions, type TableColumn } from '@/features/admin/templates/template-config';
import { OWNER_ELIGIBLE_SEMANTICS, type FieldSemantic } from '@/features/admin/templates/document-semantics';
import { formatDate } from '@/lib/format-date';
import { useDocumentEditorContext } from '../../../hooks/document-editor-context';
import { coerceRows, ownerUserId } from '../../../lib/table-rows';
import { resolveOwnerDisplay } from '../../../lib/owner-display';
import type { TableCellValue } from '../../../types';
import { fieldElementId } from '../types';
import { ReadAccommodations } from './read-accommodations';
import { ReadGoals } from './read-goals';
import { ReadServices } from './read-services';
import { ReadTransitionList } from './read-transition-list';
import type { ReadFieldRendererProps } from './types';

/**
 * Read view for a Table field. Accommodations, transition, goals and services
 * rows each get their own layout (`ReadAccommodations` / `ReadTransitionList`
 * / `ReadGoals` / `ReadServices`); everything else falls through to this
 * generic one-row-per-entry table, which appends an Owner column for any
 * owner-eligible semantic (plan 2026-10-02-002).
 */
export function ReadTable(props: ReadFieldRendererProps) {
  const { field } = props;
  const config = parseConfig(field.fieldType, field.configJson);
  const semantic = config.kind === 'Table' ? config.semantic : undefined;

  if (semantic === 'accommodations') return <ReadAccommodations {...props} />;
  if (semantic === 'transition') return <ReadTransitionList {...props} />;
  if (semantic === 'goals') return <ReadGoals {...props} />;
  if (semantic === 'services') return <ReadServices {...props} />;
  return <ReadTableGeneric {...props} semantic={semantic} />;
}

function ReadTableGeneric({ field, value, semantic }: ReadFieldRendererProps & { semantic: FieldSemantic | undefined }) {
  // `educator` alongside `document-authoring`: `resolveOwnerDisplay` below
  // renders a `teamRoleLabel` (`educator:teamRole.*`, staff-only) — this
  // hook call is what makes a language switch re-render once that
  // namespace's Spanish loads.
  const { t } = useTranslation(['document-authoring', 'educator']);
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const rows = coerceRows(value);
  const editor = useDocumentEditorContext();
  const ownerEligible = semantic != null && OWNER_ELIGIBLE_SEMANTICS.has(semantic);

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || t('readShared.untitledField')}</h3>
      {rows.length === 0 ? (
        <p className="mt-0.5 text-[15px] italic text-brand-slate-500">{t('readTable.noRowsYet')}</p>
      ) : (
        <div className="mt-1.5 overflow-x-auto rounded-card border border-brand-slate-200">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">{field.label || t('readTable.tableFieldFallback')}</caption>
            <thead>
              <tr className="bg-brand-slate-50">
                {columns.map((c) => (
                  <th
                    key={c.columnKey}
                    scope="col"
                    className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600"
                  >
                    {c.label || t('readTable.column')}
                  </th>
                ))}
                {ownerEligible && (
                  <th scope="col" className="border-b border-brand-slate-200 px-3 py-2 text-left text-[13px] font-medium text-brand-slate-600">
                    {t('readTable.owner')}
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
  if (column.type === 'Checkbox') return value === true ? i18n.t('document-authoring:readCheckbox.yes') : i18n.t('document-authoring:readCheckbox.no');
  if (column.type === 'Date') return typeof value === 'string' && value ? formatDate(value) : '—';
  if (column.type === 'Select') {
    const str = typeof value === 'string' ? value : '';
    if (!str) return '—';
    const label = readColumnOptions(column.configJson).find((o) => o.value === str)?.label?.trim();
    return label || str;
  }
  return typeof value === 'string' && value.trim() ? value : '—';
}
