import { useTranslation } from 'react-i18next';
import i18n from '@/lib/i18n';
import { Card } from '@/components/ui/card';
import { Markdown } from '@/components/ui/markdown';
import { formatDate } from '@/lib/format-date';
import {
  parseConfig,
  readColumnOptions,
  type TableColumn,
} from '@/features/admin/templates/template-config';
import type {
  TemplateFieldDto,
  TemplateSectionDto,
  TemplateVersionDetailDto,
} from '@/features/admin/templates/types';
import type { TableRowValue } from '../types';

interface AuthoredVersionSnapshotProps {
  /** The pinned template tree the frozen values were captured against. Any
   *  frozen document shares this shape — an `AuthoredDocumentVersionDetailDto`
   *  and a `SharedDraftRevisionDetailDto` both structurally satisfy it. */
  templateVersion: TemplateVersionDetailDto;
  /** Frozen value-document keyed by `FieldKey` (guid). */
  values: Record<string, unknown>;
}

// Read-only render of a frozen document snapshot: walks the pinned template
// tree in order and shows each field's stored value. Immutable — no inputs.
// Small, labeled, per-`FieldType` display (mirrors iep-versions/version-snapshot).
// Shared by the educator authored-version pages and the parent shared-draft
// review page (see `FieldValueDisplay`, exported for a custom per-field layout).
// Translated via the EAGER `document-authoring-shared` namespace (not the
// staff-only `document-authoring` one) — see `docs/i18n/README.md`'s
// "Staff and admin namespaces" on why: a parent route (the shared-draft
// review page, the parent authored-version viewer) renders this component
// too, and must never depend on the staff-only lazy chunk for it.
export function AuthoredVersionSnapshot({ templateVersion, values }: AuthoredVersionSnapshotProps) {
  const { t } = useTranslation('document-authoring-shared');
  const sections = [...templateVersion.sections].sort((a, b) => a.displayOrder - b.displayOrder);

  if (sections.length === 0) {
    return <p className="text-sm text-brand-slate-500">{t('noSections')}</p>;
  }

  return (
    <div className="space-y-6" data-testid="authored-version-snapshot">
      {sections.map((section) => (
        <SectionBlock key={section.id} section={section} values={values} />
      ))}
    </div>
  );
}

function SectionBlock({
  section,
  values,
}: {
  section: TemplateSectionDto;
  values: Record<string, unknown>;
}) {
  const { t } = useTranslation('document-authoring-shared');
  const fields = [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder);
  return (
    <section data-testid={`snapshot-section-${section.id}`}>
      <h2 className="mb-3 font-serif text-lg text-brand-slate-800">
        {section.title || t('untitledSection')}
      </h2>
      <Card className="space-y-4">
        {fields.length === 0 ? (
          <p className="text-sm text-brand-slate-500">{t('noFields')}</p>
        ) : (
          fields.map((field) => (
            <FieldValueDisplay key={field.id} field={field} value={values[field.fieldKey]} />
          ))
        )}
      </Card>
    </section>
  );
}

/** One field's frozen value, per `FieldType`. Exported so a consumer that needs
 *  a custom section layout (e.g. the parent draft review's semantic-row cards)
 *  can still render "everything else" generically. */
export function FieldValueDisplay({ field, value }: { field: TemplateFieldDto; value: unknown }) {
  const { t } = useTranslation('document-authoring-shared');
  if (field.fieldType === 'Table') {
    return <TableValue field={field} value={value} />;
  }

  return (
    <div data-testid={`snapshot-field-${field.fieldKey}`}>
      <p className="text-[13px] font-medium text-brand-slate-500">
        {field.label || t('untitledField')}
      </p>
      <div className="text-sm text-brand-slate-800">{renderScalar(field, value)}</div>
    </div>
  );
}

function renderScalar(field: TemplateFieldDto, value: unknown): React.ReactNode {
  const empty = <span className="text-brand-slate-500">—</span>;

  switch (field.fieldType) {
    case 'Checkbox':
      return value === true ? i18n.t('document-authoring-shared:yes') : i18n.t('document-authoring-shared:no');
    case 'Date': {
      if (typeof value !== 'string' || !value) return empty;
      return formatDate(value);
    }
    case 'Select': {
      if (typeof value !== 'string' || !value) return empty;
      const config = parseConfig(field.fieldType, field.configJson);
      const options = config.kind === 'Select' ? config.select.options : [];
      const match = options.find((o) => o.value === value);
      return match?.label?.trim() || match?.value || value;
    }
    case 'RichText': {
      if (typeof value !== 'string' || !value) return empty;
      return <Markdown content={value} />;
    }
    case 'Text':
    default: {
      if (typeof value !== 'string' || !value) return empty;
      return <span className="whitespace-pre-wrap">{value}</span>;
    }
  }
}

function TableValue({ field, value }: { field: TemplateFieldDto; value: unknown }) {
  const { t } = useTranslation('document-authoring-shared');
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const rows = coerceRows(value);

  return (
    <div data-testid={`snapshot-field-${field.fieldKey}`}>
      <p className="mb-1 text-[13px] font-medium text-brand-slate-500">
        {field.label || t('untitledField')}
      </p>
      {rows.length === 0 ? (
        <p className="text-sm text-brand-slate-500">{t('noRows')}</p>
      ) : (
        <div className="overflow-x-auto rounded-card border border-brand-slate-200">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">{field.label || t('tableField')}</caption>
            <thead>
              <tr className="bg-brand-slate-50">
                {columns.map((c) => (
                  <th
                    key={c.columnKey}
                    scope="col"
                    className="border-b border-brand-slate-200 px-2 py-2 text-left text-[13px] font-medium text-brand-slate-600"
                  >
                    {c.label || t('column')}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => (
                <tr key={i} className="border-b border-brand-slate-100 last:border-0">
                  {columns.map((col) => (
                    <td key={col.columnKey} className="px-2 py-1.5 align-top text-brand-slate-800">
                      {renderCell(col, row[col.columnKey])}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

/** Exported so a custom table-row layout (the parent draft review's semantic
 *  cards) can render a non-primary column the same way this snapshot does. */
export function CellValue({ column, value }: { column: TableColumn; value: unknown }) {
  return <>{renderCell(column, value)}</>;
}

function renderCell(column: TableColumn, value: unknown): React.ReactNode {
  const empty = <span className="text-brand-slate-500">—</span>;
  switch (column.type) {
    case 'Checkbox':
      return value === true ? i18n.t('document-authoring-shared:yes') : i18n.t('document-authoring-shared:no');
    case 'Date':
      return typeof value === 'string' && value ? formatDate(value) : empty;
    case 'Select': {
      if (typeof value !== 'string' || !value) return empty;
      const match = readColumnOptions(column.configJson).find((o) => o.value === value);
      return match?.label?.trim() || match?.value || value;
    }
    default:
      return typeof value === 'string' && value ? value : empty;
  }
}

function coerceRows(value: unknown): TableRowValue[] {
  if (!Array.isArray(value)) return [];
  return value.filter((r): r is TableRowValue => typeof r === 'object' && r !== null);
}
