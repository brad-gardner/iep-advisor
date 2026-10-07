import i18n from '@/lib/i18n';
import { parseConfig } from '@/features/admin/templates/template-config';
import type { ColumnSemantic, FieldSemantic } from '@/features/admin/templates/document-semantics';
import type { TemplateFieldDto } from '@/features/admin/templates/types';

/** The three semantics the parent review page renders as individual cards
 *  (Explain / Ask a question / Respond) rather than a generic table. */
export const CARD_ROW_SEMANTICS: ReadonlySet<FieldSemantic> = new Set(['goals', 'services', 'accommodations']);

/** The column semantic that supplies a row's primary display text, per field semantic. */
const PRIMARY_COLUMN_SEMANTIC: Partial<Record<FieldSemantic, ColumnSemantic>> = {
  goals: 'goalText',
  services: 'serviceType',
  accommodations: 'accommodation',
};

export type Row = Record<string, unknown>;

/** Whether this field is a Table configured with a card-rendered semantic. */
export function isCardRowField(field: TemplateFieldDto): FieldSemantic | null {
  if (field.fieldType !== 'Table') return null;
  const config = parseConfig(field.fieldType, field.configJson);
  if (config.kind !== 'Table' || !config.semantic) return null;
  return CARD_ROW_SEMANTICS.has(config.semantic) ? config.semantic : null;
}

export function tableColumns(field: TemplateFieldDto) {
  const config = parseConfig(field.fieldType, field.configJson);
  return config.kind === 'Table' ? config.table.columns : [];
}

export function primaryColumnKey(field: TemplateFieldDto, semantic: FieldSemantic): string | undefined {
  const columnSemantic = PRIMARY_COLUMN_SEMANTIC[semantic];
  if (!columnSemantic) return undefined;
  return tableColumns(field).find((c) => c.semantic === columnSemantic)?.columnKey;
}

export function coerceRows(value: unknown): Row[] {
  return Array.isArray(value) ? value.filter((r): r is Row => typeof r === 'object' && r !== null) : [];
}

/** A row's stable identity (`_rowId`), or `null` for a legacy row without one. */
export function rowId(row: Row): string | null {
  const id = row._rowId;
  return typeof id === 'string' ? id : null;
}

export type RowChangeState = 'added' | 'changed' | null;

// Lives here (a `.ts` file, not `.tsx`) rather than inline in
// `frozen-section-list.tsx`'s render body: `i18next/no-literal-string`'s
// `jsx-only` mode still flags a plain string literal in a ternary sitting in
// a component's render scope, even one that's a lookup tag (not displayed
// text) assigned to a `const` well before any JSX — same gotcha as
// `analysis-tab.tsx`'s sidebar-key comment. Pulling the ternary out into an
// ordinary function in a non-JSX file sidesteps it entirely, and reads
// better besides.
export function rowChangeState(
  id: string | null,
  fieldKey: string,
  addedRowKeys: ReadonlySet<string>,
  changedRowKeys: ReadonlySet<string>
): RowChangeState {
  if (!id) return null;
  if (addedRowKeys.has(`${fieldKey}:${id}`)) return 'added';
  if (changedRowKeys.has(`${fieldKey}:${id}`)) return 'changed';
  return null;
}

// Plain function over `i18n.t` (not `useTranslation`), same shape as
// `meetingTypeLabel`/`inviteStatusLabel` — this is a non-component helper
// called from render bodies, not a hook.
export function rowLabel(row: Row, primaryKey: string | undefined, index: number): string {
  const text = primaryKey ? row[primaryKey] : undefined;
  if (typeof text === 'string' && text.trim()) return text.trim();
  return i18n.t('shared-drafts:frozenSections.rowFallback', { number: index + 1 });
}
