import { ROW_CARRIED_FROM_KEY, ROW_ID_KEY } from '@/features/admin/templates/document-semantics';
import type { TableColumn } from '@/features/admin/templates/template-config';
import type { TableRowValue } from '../types';

/**
 * A table row with a client-side React identity. `key` is assigned once when
 * the row first appears (from the server `_rowId` when it has one, else a
 * temporary client key) and NEVER changes afterwards — changing a React key
 * remounts the row and drops the input the user is typing in. The server's
 * `_rowId` lives only inside `cells`.
 */
export interface KeyedRow {
  key: string;
  cells: TableRowValue;
}

let rowSeq = 0;
/** Temporary client key for a row the server has not seen yet. */
export function nextRowKey(): string {
  rowSeq += 1;
  return `row-${rowSeq}`;
}

export interface CarriedFrom {
  versionId: number;
  rowId: string;
  label?: string;
  date?: string;
}

/** Provenance stamped by prefill on rows carried from a prior finalized version. */
export function carriedFrom(row: KeyedRow): CarriedFrom | undefined {
  return readCarriedFrom(row.cells[ROW_CARRIED_FROM_KEY]);
}

/** Shape guard for a raw `_carriedFrom` cell value (shared with the completeness rules). */
export function readCarriedFrom(raw: unknown): CarriedFrom | undefined {
  if (!raw || typeof raw !== 'object') return undefined;
  const r = raw as Record<string, unknown>;
  if (typeof r.versionId !== 'number' || typeof r.rowId !== 'string') return undefined;
  return {
    versionId: r.versionId,
    rowId: r.rowId,
    label: typeof r.label === 'string' ? r.label : undefined,
    date: typeof r.date === 'string' ? r.date : undefined,
  };
}

export function rowId(row: KeyedRow): string | undefined {
  const id = row.cells[ROW_ID_KEY];
  return typeof id === 'string' && id ? id : undefined;
}

/** Rows from a value-document. Persisted rows carry `_rowId`; use it as the key. */
export function coerceRows(value: unknown): KeyedRow[] {
  if (!Array.isArray(value)) return [];
  return value
    .filter((r): r is TableRowValue => typeof r === 'object' && r !== null)
    .map((cells) => {
      const id = cells[ROW_ID_KEY];
      return { key: typeof id === 'string' && id ? id : nextRowKey(), cells };
    });
}

export function emptyCells(columns: TableColumn[]): TableRowValue {
  const row: TableRowValue = {};
  for (const col of columns) row[col.columnKey] = col.type === 'Checkbox' ? false : '';
  return row;
}

/**
 * After a save, adopt the server-assigned `_rowId`s into the rows that were
 * SENT (matched by their client key against the sent array, index-for-index
 * with the server response), never by position in the current state. Rows
 * removed while the save was in flight are simply absent from `current`; rows
 * added after the request have no entry in `sent` and are left untouched; rows
 * that already carry an id keep it. Keys never change.
 */
export function adoptRowIds(current: KeyedRow[], sent: KeyedRow[], saved: unknown): KeyedRow[] {
  if (!Array.isArray(saved)) return current;
  const idByKey = new Map<string, string>();
  sent.forEach((row, i) => {
    const savedRow = saved[i];
    const id = savedRow && typeof savedRow === 'object' ? (savedRow as TableRowValue)[ROW_ID_KEY] : undefined;
    if (typeof id === 'string' && id) idByKey.set(row.key, id);
  });
  if (idByKey.size === 0) return current;

  let changed = false;
  const next = current.map((row) => {
    if (rowId(row)) return row;
    const id = idByKey.get(row.key);
    if (!id) return row;
    changed = true;
    return { key: row.key, cells: { ...row.cells, [ROW_ID_KEY]: id } };
  });
  return changed ? next : current;
}
