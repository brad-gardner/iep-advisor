import { useTranslation } from 'react-i18next';
import { Card } from '@/components/ui/card';
import { RichTextEditor } from '@/components/ui/rich-text-editor';
import type { TemplateFieldDto, TemplateSectionDto } from '../types';
import { parseConfig, readColumnOptions, type TableColumn } from '../template-config';

/**
 * Read-only preview of the template as the form an educator would see. Every
 * control is disabled — this is a layout preview, not a working editor. (PDF
 * preview is deferred to Phase 4.) Field/section/column LABELS themselves
 * (`field.label`, `section.title`, `column.label`) are district-authored
 * template content, never translated — only this file's own UI chrome
 * (fallback placeholders, "Select…", etc.) is.
 */
export function FormPreview({ sections }: { sections: TemplateSectionDto[] }) {
  const { t } = useTranslation('admin');
  if (sections.length === 0) {
    return <p className="text-sm text-brand-slate-500">{t('templates.formPreview.emptyMessage')}</p>;
  }

  return (
    <div className="space-y-6" aria-label={t('templates.formPreview.ariaLabel')}>
      {sections.map((section) => (
        <Card key={section.id} data-testid={`preview-section-${section.id}`}>
          <h3 className="mb-4 font-serif text-base text-brand-slate-800">
            {section.title || t('templates.formPreview.untitledSection')}
          </h3>
          {section.fields.length === 0 ? (
            <p className="text-sm text-brand-slate-500">{t('templates.formPreview.noFields')}</p>
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
  const { t } = useTranslation('admin');
  const fieldId = `preview-field-${field.id}`;
  const config = parseConfig(field.fieldType, field.configJson);
  const untitledField = t('templates.formPreview.untitledField');

  const labelNode = (
    <label htmlFor={fieldId} className={labelClass}>
      {field.label || untitledField}
      {field.required && (
        <span className="ml-1 text-brand-danger-700" aria-hidden="true">
          *
        </span>
      )}
      {field.required && <span className="sr-only"> {t('templates.formPreview.requiredSuffix')}</span>}
    </label>
  );

  if (config.kind === 'Checkbox') {
    return (
      <div className="flex items-center gap-2">
        <input id={fieldId} type="checkbox" disabled className="h-4 w-4 rounded border-brand-slate-300" />
        <label htmlFor={fieldId} className="text-[13px] font-medium text-brand-slate-600">
          {field.label || untitledField}
          {field.required && <span className="sr-only"> {t('templates.formPreview.requiredSuffix')}</span>}
        </label>
      </div>
    );
  }

  return (
    <div>
      {labelNode}
      {config.kind === 'Text' && <input id={fieldId} type="text" disabled className={inputClass} />}
      {config.kind === 'RichText' && (
        <RichTextEditor
          id={fieldId}
          value=""
          onChange={() => {}}
          disabled
          minRows={3}
          aria-label={field.label || untitledField}
        />
      )}
      {config.kind === 'Date' && <input id={fieldId} type="date" disabled className={inputClass} />}
      {config.kind === 'Select' && (
        <select id={fieldId} disabled className={inputClass}>
          <option>{t('templates.formPreview.selectPlaceholder')}</option>
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
  const { t } = useTranslation('admin');
  const untitledColumn = t('templates.formPreview.untitledColumn');
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
                {c.label || untitledColumn}
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
  const { t } = useTranslation('admin');
  const untitledColumn = t('templates.formPreview.untitledColumn');
  switch (column.type) {
    case 'Checkbox':
      return <input type="checkbox" disabled className="h-4 w-4 rounded border-brand-slate-300" />;
    case 'Date':
      return <input type="date" disabled className={`${inputClass} py-1`} />;
    case 'Select':
      return (
        <select
          disabled
          className={`${inputClass} py-1`}
          aria-label={t('templates.formPreview.columnValueLabel', { column: column.label || untitledColumn })}
        >
          <option>{t('templates.formPreview.selectPlaceholder')}</option>
          {readColumnOptions(column.configJson).map((o, i) => (
            <option key={i}>{o.label?.trim() || o.value}</option>
          ))}
        </select>
      );
    default:
      return (
        <input
          type="text"
          disabled
          className={`${inputClass} py-1`}
          aria-label={t('templates.formPreview.columnValueLabel', { column: column.label || untitledColumn })}
        />
      );
  }
}
