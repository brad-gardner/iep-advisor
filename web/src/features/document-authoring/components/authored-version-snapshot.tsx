import { Card } from '@/components/ui/card';
import {
  parseConfig,
  readColumnOptions,
  type TableColumn,
} from '@/features/admin/templates/template-config';
import type {
  TemplateFieldDto,
  TemplateSectionDto,
} from '@/features/admin/templates/types';
import type { AuthoredDocumentVersionDetailDto, TableRowValue } from '../types';

interface AuthoredVersionSnapshotProps {
  version: AuthoredDocumentVersionDetailDto;
}

// Read-only render of a frozen authored version: walks the pinned template tree
// in order and shows each field's stored value. Immutable — no inputs. Small,
// labeled, per-`FieldType` display (mirrors iep-versions/version-snapshot).
export function AuthoredVersionSnapshot({ version }: AuthoredVersionSnapshotProps) {
  const sections = [...version.templateVersion.sections].sort(
    (a, b) => a.displayOrder - b.displayOrder
  );

  if (sections.length === 0) {
    return <p className="text-sm text-brand-slate-400">This template has no sections.</p>;
  }

  return (
    <div className="space-y-6" data-testid="authored-version-snapshot">
      {sections.map((section) => (
        <SectionBlock key={section.id} section={section} values={version.values} />
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
  const fields = [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder);
  return (
    <section data-testid={`snapshot-section-${section.id}`}>
      <h2 className="mb-3 font-serif text-lg text-brand-slate-800">
        {section.title || 'Untitled section'}
      </h2>
      <Card className="space-y-4">
        {fields.length === 0 ? (
          <p className="text-sm text-brand-slate-400">No fields.</p>
        ) : (
          fields.map((field) => (
            <FieldValue key={field.id} field={field} value={values[field.fieldKey]} />
          ))
        )}
      </Card>
    </section>
  );
}

function FieldValue({ field, value }: { field: TemplateFieldDto; value: unknown }) {
  if (field.fieldType === 'Table') {
    return <TableValue field={field} value={value} />;
  }

  return (
    <div data-testid={`snapshot-field-${field.fieldKey}`}>
      <p className="text-[13px] font-medium text-brand-slate-500">
        {field.label || 'Untitled field'}
      </p>
      <div className="text-sm text-brand-slate-800">{renderScalar(field, value)}</div>
    </div>
  );
}

function renderScalar(field: TemplateFieldDto, value: unknown): React.ReactNode {
  const empty = <span className="text-brand-slate-400">—</span>;

  switch (field.fieldType) {
    case 'Checkbox':
      return value === true ? 'Yes' : 'No';
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
    case 'RichText':
    case 'Text':
    default: {
      if (typeof value !== 'string' || !value) return empty;
      return <span className="whitespace-pre-wrap">{value}</span>;
    }
  }
}

function TableValue({ field, value }: { field: TemplateFieldDto; value: unknown }) {
  const config = parseConfig(field.fieldType, field.configJson);
  const columns = config.kind === 'Table' ? config.table.columns : [];
  const rows = coerceRows(value);

  return (
    <div data-testid={`snapshot-field-${field.fieldKey}`}>
      <p className="mb-1 text-[13px] font-medium text-brand-slate-500">
        {field.label || 'Untitled field'}
      </p>
      {rows.length === 0 ? (
        <p className="text-sm text-brand-slate-400">No rows.</p>
      ) : (
        <div className="overflow-x-auto rounded-card border border-brand-slate-200">
          <table className="w-full border-collapse text-sm">
            <caption className="sr-only">{field.label || 'Table field'}</caption>
            <thead>
              <tr className="bg-brand-slate-50">
                {columns.map((c) => (
                  <th
                    key={c.columnKey}
                    scope="col"
                    className="border-b border-brand-slate-200 px-2 py-2 text-left text-[13px] font-medium text-brand-slate-600"
                  >
                    {c.label || 'Column'}
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

function renderCell(column: TableColumn, value: unknown): React.ReactNode {
  const empty = <span className="text-brand-slate-400">—</span>;
  switch (column.type) {
    case 'Checkbox':
      return value === true ? 'Yes' : 'No';
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

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
