import { parseConfig } from '@/features/admin/templates/template-config';
import { ROW_CARRIED_FROM_KEY, ROW_CONFIRMED_KEY, ROW_ID_KEY, type ColumnSemantic } from '@/features/admin/templates/document-semantics';
import type { TemplateFieldDto, TemplateVersionDetailDto } from '../types';
import { readCarriedFrom } from './table-rows';

export type CompletenessSeverity = 'required' | 'advisory';

export interface CompletenessItem {
  /** Stable key for React lists. */
  key: string;
  severity: CompletenessSeverity;
  message: string;
  /** Field to scroll to when clicked. */
  fieldKey: string;
  /** Numeric template field id (drives the control's DOM id). */
  fieldId: number;
  sectionId: number;
}

export interface CompletenessSummary {
  items: CompletenessItem[];
  /** 0–100: filled fields (any non-empty value) over all fields. */
  percent: number;
  filledCount: number;
  totalCount: number;
}

type Row = Record<string, unknown>;

function isBlank(v: unknown): boolean {
  if (v == null) return true;
  if (typeof v === 'string') return v.replace(/<[^>]+>/g, '').trim() === '';
  if (typeof v === 'boolean') return false;
  if (Array.isArray(v)) return v.length === 0;
  return false;
}

function rows(v: unknown): Row[] {
  return Array.isArray(v) ? v.filter((r): r is Row => typeof r === 'object' && r !== null) : [];
}

function cellBlank(row: Row, columnKey: string | undefined): boolean {
  if (!columnKey) return false;
  return isBlank(row[columnKey]);
}

function rowLabel(row: Row, primaryKey: string | undefined, index: number): string {
  const text = primaryKey ? row[primaryKey] : undefined;
  if (typeof text === 'string' && text.trim()) {
    const t = text.trim();
    return `"${t.length > 40 ? `${t.slice(0, 40)}…` : t}"`;
  }
  return `row ${index + 1}`;
}

/**
 * Advisory, semantic-aware completeness for a document draft. Required-field
 * gaps mirror what finalize enforces server-side; everything else is coaching
 * (a goal without a baseline, a service without a frequency) and never blocks.
 */
export function computeCompleteness(
  template: TemplateVersionDetailDto,
  values: Record<string, unknown>
): CompletenessSummary {
  const items: CompletenessItem[] = [];
  let filled = 0;
  let total = 0;

  const sections = [...template.sections].sort((a, b) => a.displayOrder - b.displayOrder);
  for (const section of sections) {
    const fields: TemplateFieldDto[] = [...section.fields].sort((a, b) => a.displayOrder - b.displayOrder);
    for (const field of fields) {
      total += 1;
      const value = values[field.fieldKey];
      const blank = isBlank(value);
      if (!blank) filled += 1;

      if (field.required && blank) {
        items.push({
          key: `req-${field.fieldKey}`,
          severity: 'required',
          message: `${field.label || 'Untitled field'} is required`,
          fieldKey: field.fieldKey,
          fieldId: field.id,
          sectionId: section.id,
        });
      }

      const config = parseConfig(field.fieldType, field.configJson);
      if (config.kind !== 'Table') continue;
      const semantic = config.semantic;
      const cols = config.table.columns;
      const col = (s: ColumnSemantic) => cols.find((c) => c.semantic === s)?.columnKey;

      const tableRows = rows(value);
      const stale = tableRows.filter((r) => readCarriedFrom(r[ROW_CARRIED_FROM_KEY]) != null && r[ROW_CONFIRMED_KEY] !== true).length;
      if (stale > 0) {
        items.push({
          key: `stale-${field.fieldKey}`,
          severity: 'advisory',
          message: `${stale} carried-forward ${stale === 1 ? 'row' : 'rows'} in ${field.label || 'this table'} not yet reviewed`,
          fieldKey: field.fieldKey,
          fieldId: field.id,
          sectionId: section.id,
        });
      }
      if (semantic === 'goals') {
        if (tableRows.length === 0) {
          items.push({ key: `goals-none-${field.fieldKey}`, severity: 'advisory', message: 'No annual goals yet', fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
        }
        tableRows.forEach((row, i) => {
          const label = rowLabel(row, col('goalText'), i);
          if (cellBlank(row, col('goalText'))) items.push({ key: `g-text-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Goal ${label} has no goal text`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
          if (cellBlank(row, col('baseline'))) items.push({ key: `g-base-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Goal ${label} has no baseline`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
          if (cellBlank(row, col('measurementMethod'))) items.push({ key: `g-meas-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Goal ${label} has no measurement method`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
          if (cellBlank(row, col('targetCriteria'))) items.push({ key: `g-target-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Goal ${label} has no target criteria`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
        });
      } else if (semantic === 'services') {
        tableRows.forEach((row, i) => {
          const label = rowLabel(row, col('serviceType'), i);
          if (cellBlank(row, col('frequency'))) items.push({ key: `s-freq-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Service ${label} has no frequency`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
          if (cellBlank(row, col('duration'))) items.push({ key: `s-dur-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Service ${label} has no duration`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
          if (cellBlank(row, col('providerRole'))) items.push({ key: `s-prov-${row[ROW_ID_KEY] ?? i}`, severity: 'advisory', message: `Service ${label} has no provider role`, fieldKey: field.fieldKey, fieldId: field.id, sectionId: section.id });
        });
      }
    }
  }

  return {
    items,
    percent: total === 0 ? 0 : Math.round((filled / total) * 100),
    filledCount: filled,
    totalCount: total,
  };
}
