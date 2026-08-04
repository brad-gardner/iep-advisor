import { Card } from '@/components/ui/card';
import type { TemplateFieldDto, TemplateSectionDto } from '../types';
import { parseConfig, readColumnOptions, type TableColumn } from '../template-config';

/**
 * Read-only preview of the template as the form an educator would see. Every
 * control is disabled — this is a layout preview, not a working editor. (PDF
 * preview is deferred to Phase 4.)
 */
export function FormPreview({ sections }: { sections: TemplateSectionDto[] }) {
  if (sections.length === 0) {
    return <p className="text-sm text-brand-slate-400">Add a section to see the form preview.</p>;
  }

  return (
    <div className="space-y-6" aria-label="Form preview">
      {sections.map((section) => (
        <Card key={section.id} data-testid={`preview-section-${section.id}`}>
          <h3 className="mb-4 font-serif text-base text-brand-slate-800">
            {section.title || 'Untitled section'}
          </h3>
          {section.fields.length === 0 ? (
            <p className="text-sm text-brand-slate-400">No fields.</p>
          ) : (
            <div className="space-y-4">
              {section.fields.map((field) => (
                <PreviewField key={field.id} field={field} />
              ))}
            </div>
          )}
        </Card>
      ))}
    </div>
  );
}

const labelClass = 'block text-[13px] font-medium text-brand-slate-600 mb-1';
const inputClass =
  'w-full px-3 py-2 bg-brand-slate-50 rounded-input text-brand-slate-500 text-sm border border-brand-slate-200';

function PreviewField({ field }: { field: TemplateFieldDto }) {
  const fieldId = `preview-field-${field.id}`;
  const config = parseConfig(field.fieldType, field.configJson);

  const labelNode = (
    <label htmlFor={fieldId} className={labelClass}>
      {field.label || 'Untitled field'}
      {field.required && (
        <span className="ml-1 text-brand-danger-700" aria-hidden="true">
          *
        </span>
      )}
      {field.required && <span className="sr-only"> (required)</span>}
    </label>
  );

  if (config.kind === 'Checkbox') {
    return (
      <div className="flex items-center gap-2">
        <input id={fieldId} type="checkbox" disabled className="h-4 w-4 rounded border-brand-slate-300" />
        <label htmlFor={fieldId} className="text-[13px] font-medium text-brand-slate-600">
          {field.label || 'Untitled field'}
          {field.required && <span className="sr-only"> (required)</span>}
        </label>
      </div>
    );
  }

  return (
    <div>
      {labelNode}
      {config.kind === 'Text' && <input id={fieldId} type="text" disabled className={inputClass} />}
      {config.kind === 'RichText' && (
        <textarea id={fieldId} disabled rows={3} className={`${inputClass} resize-none`} />
      )}
      {config.kind === 'Date' && <input id={fieldId} type="date" disabled className={inputClass} />}
      {config.kind === 'Select' && (
        <select id={fieldId} disabled className={inputClass}>
          <option>Select…</option>
          {config.select.options.map((o, i) => (
            <option key={i}>{o.label?.trim() || o.value}</option>
          ))}
        </select>
      )}
      {config.kind === 'Table' && <PreviewTable columns={config.table.columns} />}
    </div>
  );
}

function PreviewTable({ columns }: { columns: TableColumn[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr>
            {columns.map((c) => (
              <th
                key={c.columnKey}
                className="border border-brand-slate-200 bg-brand-slate-50 px-2 py-1 text-left text-[13px] font-medium text-brand-slate-600"
              >
                {c.label || 'Column'}
                {c.required && (
                  <span className="ml-1 text-brand-danger-700" aria-hidden="true">
                    *
                  </span>
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          <tr>
            {columns.map((c) => (
              <td key={c.columnKey} className="border border-brand-slate-200 px-2 py-1">
                <PreviewCell column={c} />
              </td>
            ))}
          </tr>
        </tbody>
      </table>
    </div>
  );
}

function PreviewCell({ column }: { column: TableColumn }) {
  switch (column.type) {
    case 'Checkbox':
      return <input type="checkbox" disabled className="h-4 w-4 rounded border-brand-slate-300" />;
    case 'Date':
      return <input type="date" disabled className={`${inputClass} py-1`} />;
    case 'Select':
      return (
        <select disabled className={`${inputClass} py-1`} aria-label={`${column.label || 'Column'} value`}>
          <option>Select…</option>
          {readColumnOptions(column.configJson).map((o, i) => (
            <option key={i}>{o.label?.trim() || o.value}</option>
          ))}
        </select>
      );
    default:
      return <input type="text" disabled className={`${inputClass} py-1`} aria-label={`${column.label || 'Column'} value`} />;
  }
}
