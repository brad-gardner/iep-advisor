import i18n from '@/lib/i18n';
import { readColumnOptions, type TableColumn } from '@/features/admin/templates/template-config';
import type { KeyedRow } from './table-rows';

export function ungroupedLabel(): string {
  return i18n.t('document-authoring:groupRows.uncategorized');
}

/**
 * Groups rows by a column's resolved display label — a Select option's label, or
 * the raw text for a Text column — preserving first-seen order (the order items
 * were entered, not alphabetical). A row with a blank value for the column falls
 * into `fallbackLabel`. Used by the accommodations read view (grouped by
 * category); plan 2026-10-02-002.
 */
export function groupRowsByColumn(
  rows: KeyedRow[],
  column: TableColumn | undefined,
  fallbackLabel: string = ungroupedLabel()
): Array<{ label: string; rows: KeyedRow[] }> {
  const labelFor = (row: KeyedRow): string => {
    if (!column) return fallbackLabel;
    const raw = row.cells[column.columnKey];
    const str = typeof raw === 'string' ? raw.trim() : '';
    if (!str) return fallbackLabel;
    if (column.type === 'Select') {
      const option = readColumnOptions(column.configJson).find((o) => o.value === str);
      return option?.label?.trim() || str;
    }
    return str;
  };

  const order: string[] = [];
  const byLabel = new Map<string, KeyedRow[]>();
  for (const row of rows) {
    const label = labelFor(row);
    const existing = byLabel.get(label);
    if (existing) {
      existing.push(row);
    } else {
      byLabel.set(label, [row]);
      order.push(label);
    }
  }
  return order.map((label) => ({ label, rows: byLabel.get(label) as KeyedRow[] }));
}

/** Resolves a single column's display label for one row — a Select option's
 *  label, or the raw text — or `fallbackLabel` when blank. Used by the
 *  transition read view (each item is labeled by its own goal area, not merged
 *  with others sharing the same area). */
export function columnDisplayLabel(row: KeyedRow, column: TableColumn | undefined, fallbackLabel: string): string {
  if (!column) return fallbackLabel;
  const raw = row.cells[column.columnKey];
  const str = typeof raw === 'string' ? raw.trim() : '';
  if (!str) return fallbackLabel;
  if (column.type === 'Select') {
    const option = readColumnOptions(column.configJson).find((o) => o.value === str);
    return option?.label?.trim() || str;
  }
  return str;
}
