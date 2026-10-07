// Typed models for the per-`FieldType` `configJson` payload, plus parse /
// serialize / client-side validate helpers. The backend also validates on save
// (Select needs >=1 unique option, Table needs >=1 non-Table column, etc.); we
// mirror those rules client-side so autosave never fires an obviously-invalid
// config and the builder can show inline hints.

import type { FieldType } from './types';
import { isColumnSemantic, isFieldSemantic, type ColumnSemantic, type FieldSemantic } from './document-semantics';

/** Column types allowed inside a Table field (no nested Table / RichText). */
export type TableColumnType = 'Text' | 'Date' | 'Select' | 'Checkbox';

export interface SelectOption {
  value: string;
  label?: string;
}

export interface TableColumn {
  /** Generated client-side (uuid) so reorder/rename never remaps stored cells. */
  columnKey: string;
  type: TableColumnType;
  label: string;
  required: boolean;
  /** For Select columns: stringified `{ options }`. */
  configJson?: string;
  /** Optional semantic tag naming what the column holds (e.g. `baseline`). */
  semantic?: ColumnSemantic;
}

export interface TextConfig {
  maxLength?: number;
}

export interface DateConfig {
  format?: string;
}

export interface SelectConfig {
  options: SelectOption[];
}

export interface TableConfig {
  columns: TableColumn[];
  minRows?: number;
  maxRows?: number;
}

/** Optional semantic tag carried by every config kind. */
interface WithSemantic {
  semantic?: FieldSemantic;
}

/** Discriminated view of a field's config keyed by its `FieldType`. */
export type FieldConfig =
  | ({ kind: 'Text'; text: TextConfig } & WithSemantic)
  | ({ kind: 'RichText' } & WithSemantic)
  | ({ kind: 'Date'; date: DateConfig } & WithSemantic)
  | ({ kind: 'Select'; select: SelectConfig } & WithSemantic)
  | ({ kind: 'Checkbox' } & WithSemantic)
  | ({ kind: 'Table'; table: TableConfig } & WithSemantic);

/**
 * Compile-time exhaustiveness guard for `FieldType` switches that also degrades
 * safely at runtime. In a `default:` arm `value` narrows to `never`, so adding a
 * new `FieldType` without an explicit case fails type-check here; at runtime an
 * out-of-contract type coming from the backend (client older than the API) hits
 * this instead of falling through to `undefined`, which would white-screen any
 * caller that reads `config.kind`.
 */
function unsupportedFieldType(value: never): FieldConfig {
  void value;
  return { kind: 'Text', text: {} };
}

function generateColumnKey(): string {
  // crypto.randomUUID is available in all evergreen browsers + jsdom (node 19+).
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return `col-${Math.random().toString(36).slice(2)}-${Date.now().toString(36)}`;
}

export function newTableColumn(type: TableColumnType = 'Text'): TableColumn {
  return { columnKey: generateColumnKey(), type, label: '', required: false };
}

/** A fresh, empty-but-shaped config for a given field type. */
export function defaultConfig(fieldType: FieldType): FieldConfig {
  switch (fieldType) {
    case 'Text':
      return { kind: 'Text', text: {} };
    case 'RichText':
      return { kind: 'RichText' };
    case 'Date':
      return { kind: 'Date', date: {} };
    case 'Select':
      return { kind: 'Select', select: { options: [{ value: '' }] } };
    case 'Checkbox':
      return { kind: 'Checkbox' };
    case 'Table':
      return { kind: 'Table', table: { columns: [newTableColumn()] } };
    default:
      return unsupportedFieldType(fieldType);
  }
}

// Exported (not just used internally) so `./lib/validate-config.ts` can read
// a Select field/column's raw options the same way, without duplicating the
// parsing logic — plain data helpers, not translated text, so exporting them
// has no bearing on the eager-reachability concern described above.
export function asRecord(raw: string | null | undefined): Record<string, unknown> {
  if (!raw) return {};
  try {
    const parsed: unknown = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function toNumberOrUndefined(v: unknown): number | undefined {
  return typeof v === 'number' && Number.isFinite(v) ? v : undefined;
}

export function parseSelectOptions(raw: unknown): SelectOption[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((o) => {
    const rec = (o ?? {}) as Record<string, unknown>;
    return {
      value: typeof rec.value === 'string' ? rec.value : '',
      label: typeof rec.label === 'string' ? rec.label : undefined,
    };
  });
}

function parseTableColumns(raw: unknown): TableColumn[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((c) => {
    const rec = (c ?? {}) as Record<string, unknown>;
    const type = rec.type;
    const columnType: TableColumnType =
      type === 'Date' || type === 'Select' || type === 'Checkbox' ? type : 'Text';
    return {
      columnKey: typeof rec.columnKey === 'string' && rec.columnKey ? rec.columnKey : generateColumnKey(),
      type: columnType,
      label: typeof rec.label === 'string' ? rec.label : '',
      required: rec.required === true,
      configJson: typeof rec.configJson === 'string' ? rec.configJson : undefined,
      semantic: isColumnSemantic(rec.semantic) ? rec.semantic : undefined,
    };
  });
}

/** Attach the optional field-level semantic read from a raw config record. */
function withSemantic<T extends FieldConfig>(config: T, rec: Record<string, unknown>): T {
  return isFieldSemantic(rec.semantic) ? { ...config, semantic: rec.semantic } : config;
}

/** Parse a stored `configJson` string into the typed model for `fieldType`. */
export function parseConfig(fieldType: FieldType, configJson: string | null): FieldConfig {
  const rec = asRecord(configJson);
  switch (fieldType) {
    case 'Text':
      return withSemantic({ kind: 'Text', text: { maxLength: toNumberOrUndefined(rec.maxLength) } }, rec);
    case 'RichText':
      return withSemantic({ kind: 'RichText' }, rec);
    case 'Date':
      return withSemantic(
        { kind: 'Date', date: { format: typeof rec.format === 'string' ? rec.format : undefined } },
        rec
      );
    case 'Select': {
      const options = parseSelectOptions(rec.options);
      return withSemantic(
        { kind: 'Select', select: { options: options.length ? options : [{ value: '' }] } },
        rec
      );
    }
    case 'Checkbox':
      return withSemantic({ kind: 'Checkbox' }, rec);
    case 'Table': {
      const columns = parseTableColumns(rec.columns);
      return withSemantic(
        {
          kind: 'Table',
          table: {
            columns: columns.length ? columns : [newTableColumn()],
            minRows: toNumberOrUndefined(rec.minRows),
            maxRows: toNumberOrUndefined(rec.maxRows),
          },
        },
        rec
      );
    }
    default:
      return unsupportedFieldType(fieldType);
  }
}

/** Read a Select *column's* options out of its stringified `configJson`. */
export function readColumnOptions(configJson: string | undefined): SelectOption[] {
  const opts = parseSelectOptions(asRecord(configJson).options);
  return opts.length ? opts : [{ value: '' }];
}

/** Serialize a Select column's options back into its `configJson` string. */
export function writeColumnOptions(options: SelectOption[]): string {
  return serializeSelectOptions(options);
}

function serializeSelectOptions(options: SelectOption[]): string {
  return JSON.stringify({
    options: options.map((o) => {
      const label = o.label?.trim();
      return label ? { value: o.value.trim(), label } : { value: o.value.trim() };
    }),
  });
}

/**
 * Serialize a typed config back to the `configJson` string the API expects.
 * Returns `undefined` for types that carry no config (RichText/Checkbox) and for
 * empty optional scalar config, so we don't persist noise like `{}`.
 */
export function serializeConfig(config: FieldConfig): string | undefined {
  const sem = config.semantic ? { semantic: config.semantic } : {};
  const hasSem = config.semantic != null;
  switch (config.kind) {
    case 'Text':
      return config.text.maxLength != null || hasSem
        ? JSON.stringify({ ...(config.text.maxLength != null ? { maxLength: config.text.maxLength } : {}), ...sem })
        : undefined;
    case 'RichText':
      return hasSem ? JSON.stringify(sem) : undefined;
    case 'Date':
      return config.date.format || hasSem
        ? JSON.stringify({ ...(config.date.format ? { format: config.date.format } : {}), ...sem })
        : undefined;
    case 'Select':
      return JSON.stringify({ ...JSON.parse(serializeSelectOptions(config.select.options)), ...sem });
    case 'Checkbox':
      return hasSem ? JSON.stringify(sem) : undefined;
    case 'Table':
      return JSON.stringify({
        columns: config.table.columns.map((c) => {
          const base: Record<string, unknown> = {
            columnKey: c.columnKey,
            type: c.type,
            label: c.label.trim(),
            required: c.required,
          };
          if (c.type === 'Select' && c.configJson) base.configJson = c.configJson;
          if (c.semantic) base.semantic = c.semantic;
          return base;
        }),
        ...(config.table.minRows != null ? { minRows: config.table.minRows } : {}),
        ...(config.table.maxRows != null ? { maxRows: config.table.maxRows } : {}),
        ...sem,
      });
  }
}

// `validateConfig` (and its `validateSelectOptions` helper) used to live here,
// returning plain English reason strings. Moved to `./lib/validate-config.ts`
// — imported directly from there by its one caller, `field-editor.tsx` — and
// NOT re-exported from this file: this file sits on an EAGERLY reachable
// import path (`features/shared-drafts/lib/semantic-rows.ts` imports
// `parseConfig` from here at runtime), and `lib/i18n/staff-namespace-boundary.
// test.ts` scans a whole FILE's source text for any staff-only namespace
// reference, following a static `export ... from` just as it follows an
// `import` — a re-export here would make the eager walk reach
// `./lib/validate-config.ts` too, defeating the split. See that new file's
// doc comment, and `document-semantics.ts`'s own.
