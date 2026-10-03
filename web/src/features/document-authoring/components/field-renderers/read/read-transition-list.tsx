import { parseConfig } from '@/features/admin/templates/template-config';
import { Markdown } from '@/components/ui/markdown';
import { cn } from '@/lib/cn';
import { useDocumentEditorContext } from '../../../hooks/document-editor-context';
import { coerceRows, ownerUserId } from '../../../lib/table-rows';
import { columnDisplayLabel } from '../../../lib/group-rows';
import { resolveOwnerDisplay } from '../../../lib/owner-display';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

const UNNAMED_AREA = 'Postsecondary goal area not set';

/**
 * Read view for the transition block: one item per row, each labeled by its own
 * postsecondary goal area (the `goalArea` column semantic) with its owner shown
 * alongside, and the transition services text below — plan 2026-10-02-002.
 */
export function ReadTransitionList({ field, value }: ReadFieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const areaCol = columns.find((c) => c.semantic === 'goalArea');
  const textCol = columns.find((c) => c.semantic === 'transitionServices') ?? columns.find((c) => c.type === 'Text');
  const rows = coerceRows(value);
  const editor = useDocumentEditorContext();

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
      {rows.length === 0 ? (
        <p className="mt-0.5 text-[15px] italic text-brand-slate-500">No rows yet.</p>
      ) : (
        <ul className="mt-2 space-y-3 text-sm">
          {rows.map((row) => {
            const text = textCol ? row.cells[textCol.columnKey] : undefined;
            const owner = resolveOwnerDisplay(ownerUserId(row), editor?.team);
            return (
              <li
                key={row.key}
                className="rounded-input border border-brand-slate-200 p-3"
                data-testid={`read-field-${field.fieldKey}-item-${row.key}`}
              >
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <span className="text-xs font-medium uppercase tracking-wide text-brand-slate-500">
                    {columnDisplayLabel(row, areaCol, UNNAMED_AREA)}
                  </span>
                  {owner && (
                    <span className={cn('text-brand-slate-500', owner.former && 'italic')}>Owner: {owner.label}</span>
                  )}
                </div>
                <div className="mt-1 text-brand-slate-700">
                  {typeof text === 'string' && text.trim() ? (
                    <Markdown content={text} />
                  ) : (
                    <span className="italic text-brand-slate-500">Not set</span>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
