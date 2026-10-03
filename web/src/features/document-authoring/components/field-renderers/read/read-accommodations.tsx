import { parseConfig } from '@/features/admin/templates/template-config';
import { Markdown } from '@/components/ui/markdown';
import { cn } from '@/lib/cn';
import { useDocumentEditorContext } from '../../../hooks/document-editor-context';
import { coerceRows, ownerUserId } from '../../../lib/table-rows';
import { groupRowsByColumn } from '../../../lib/group-rows';
import { resolveOwnerDisplay } from '../../../lib/owner-display';
import { fieldElementId } from '../types';
import type { ReadFieldRendererProps } from './types';

/**
 * Read view for the accommodations block: grouped by category (the `category`
 * column semantic), each item showing its accommodation text and owner — plan
 * 2026-10-02-002. Falls back to "Uncategorized" when a row has no category, or
 * when the template doesn't tag a category column at all.
 */
export function ReadAccommodations({ field, value }: ReadFieldRendererProps) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const categoryCol = columns.find((c) => c.semantic === 'category');
  const textCol = columns.find((c) => c.semantic === 'accommodation') ?? columns.find((c) => c.type === 'Text');
  const rows = coerceRows(value);
  const editor = useDocumentEditorContext();
  const groups = groupRowsByColumn(rows, categoryCol);

  return (
    <div id={fieldElementId(field.id)} data-testid={`read-field-${field.fieldKey}`}>
      <h3 className="text-[13px] font-medium text-brand-slate-500">{field.label || 'Untitled field'}</h3>
      {rows.length === 0 ? (
        <p className="mt-0.5 text-[15px] italic text-brand-slate-500">No rows yet.</p>
      ) : (
        <div className="mt-2 grid gap-6 text-sm md:grid-cols-2" data-testid={`read-field-${field.fieldKey}-groups`}>
          {groups.map((group) => (
            <div key={group.label} data-testid={`read-field-${field.fieldKey}-group-${group.label}`}>
              <h4 className="text-xs font-medium uppercase tracking-wide text-brand-slate-500">{group.label}</h4>
              <ul className="mt-2 space-y-2">
                {group.rows.map((row) => {
                  const text = textCol ? row.cells[textCol.columnKey] : undefined;
                  const owner = resolveOwnerDisplay(ownerUserId(row), editor?.team);
                  return (
                    <li key={row.key} className="flex items-start justify-between gap-4">
                      <div className="min-w-0 text-brand-slate-700">
                        {typeof text === 'string' && text.trim() ? (
                          <Markdown content={text} />
                        ) : (
                          <span className="italic text-brand-slate-500">Not set</span>
                        )}
                      </div>
                      {owner && (
                        <span
                          className={cn('shrink-0 whitespace-nowrap text-brand-slate-500', owner.former && 'italic')}
                        >
                          {owner.label}
                        </span>
                      )}
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
