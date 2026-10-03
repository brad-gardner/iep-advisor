import { ROW_ID_KEY, ROW_OBJECTIVES_KEY } from '@/features/admin/templates/document-semantics';
import type { KeyedRow } from './table-rows';

/**
 * Objective-level mirror of `table-rows.ts`'s row-identity invariants (plan
 * 2026-10-02-002, Phase 3 — goal objectives): a goal row's `_objectives` cell
 * is an ordered array of `{ _rowId, description, criteria, targetDate }`
 * (`DocumentInstanceService.CoerceObjectives`). While a goal is being edited,
 * the cell holds `KeyedObjective[]` instead of the plain JSON shape — the same
 * "rich while editing, flattened only at the wire" trick table rows don't need
 * (they ARE the top-level array) but a nested array does, so its own items can
 * carry a React key that is independent of, and survives longer than, the
 * server-assigned `_rowId`. `toPlainObjectives` flattens back to the wire shape
 * immediately before a save; nothing outside an open goal editor ever sees the
 * rich shape (read views and completeness always read saved, plain JSON).
 */
export interface ObjectiveCells {
  _rowId?: string;
  description: string;
  criteria: string;
  targetDate: string;
}

export interface KeyedObjective {
  key: string;
  cells: ObjectiveCells;
}

let objectiveSeq = 0;
/** Temporary client key for an objective the server has not seen yet. */
export function nextObjectiveKey(): string {
  objectiveSeq += 1;
  return `objective-${objectiveSeq}`;
}

export function objectiveId(o: KeyedObjective): string | undefined {
  return o.cells._rowId;
}

export function emptyObjective(): KeyedObjective {
  return { key: nextObjectiveKey(), cells: { description: '', criteria: '', targetDate: '' } };
}

/** Objectives from a goal row's `_objectives` cell (plain JSON, from the server
 *  or an as-yet-untouched row). Persisted objectives carry `_rowId`; use it as
 *  the key, same rule as `coerceRows`. */
export function coerceObjectives(value: unknown): KeyedObjective[] {
  if (!Array.isArray(value)) return [];
  return value
    .filter((o): o is Record<string, unknown> => typeof o === 'object' && o !== null)
    .map((raw) => {
      const id = typeof raw[ROW_ID_KEY] === 'string' && raw[ROW_ID_KEY] ? (raw[ROW_ID_KEY] as string) : undefined;
      return {
        key: id ?? nextObjectiveKey(),
        cells: {
          _rowId: id,
          description: typeof raw.description === 'string' ? raw.description : '',
          criteria: typeof raw.criteria === 'string' ? raw.criteria : '',
          targetDate: typeof raw.targetDate === 'string' ? raw.targetDate : '',
        },
      };
    });
}

/** Flattens the edit-time `KeyedObjective[]` back to the plain wire shape
 *  (drops the client `key`; keeps `_rowId` only when already assigned). */
export function toPlainObjectives(objectives: KeyedObjective[]): Record<string, unknown>[] {
  return objectives.map(({ cells }) => {
    const plain: Record<string, unknown> = {
      description: cells.description,
      criteria: cells.criteria,
      targetDate: cells.targetDate,
    };
    if (cells._rowId) plain[ROW_ID_KEY] = cells._rowId;
    return plain;
  });
}

/**
 * Adopts server-assigned objective ids into the objectives that were SENT
 * (matched by client key against the sent array, index-for-index with the
 * server's response for this row's `_objectives`), never by position in
 * `current` — same invariants as `adoptRowIds`: a key never changes, and an
 * objective that reduced to nothing server-side (blank description/criteria/
 * targetDate) simply gets no id back, same as it never being sent at all.
 */
export function adoptObjectiveIds(current: KeyedObjective[], sent: KeyedObjective[], saved: unknown): KeyedObjective[] {
  if (!Array.isArray(saved)) return current;
  const idByKey = new Map<string, string>();
  sent.forEach((o, i) => {
    const savedO = saved[i];
    const id = savedO && typeof savedO === 'object' ? (savedO as Record<string, unknown>)[ROW_ID_KEY] : undefined;
    if (typeof id === 'string' && id) idByKey.set(o.key, id);
  });
  if (idByKey.size === 0) return current;

  let changed = false;
  const next = current.map((o) => {
    if (o.cells._rowId) return o;
    const id = idByKey.get(o.key);
    if (!id) return o;
    changed = true;
    return { key: o.key, cells: { ...o.cells, _rowId: id } };
  });
  return changed ? next : current;
}

/**
 * Row-level glue: after `adoptRowIds` has adopted each goal row's own
 * `_rowId`, this adopts objective ids WITHIN each row's `_objectives` cell —
 * matching the row by its (already-stable) client key against the exact rows
 * that were sent, then the row's sent/saved `_objectives` arrays by position,
 * per `adoptObjectiveIds`. A row absent from `sent` (added after the request)
 * or whose `_objectives` cell isn't the rich `KeyedObjective[]` shape (never
 * touched by the objectives editor) is left untouched.
 */
export function adoptRowObjectiveIds(current: KeyedRow[], sent: KeyedRow[], saved: unknown): KeyedRow[] {
  if (!Array.isArray(saved)) return current;

  let changed = false;
  const next = current.map((row) => {
    const sentIndex = sent.findIndex((r) => r.key === row.key);
    if (sentIndex === -1) return row;
    const sentObjectives = sent[sentIndex].cells[ROW_OBJECTIVES_KEY];
    if (!Array.isArray(sentObjectives) || sentObjectives.length === 0) return row;
    const currentObjectives = row.cells[ROW_OBJECTIVES_KEY];
    if (!Array.isArray(currentObjectives)) return row;

    const savedRow = saved[sentIndex];
    const savedObjectives =
      savedRow && typeof savedRow === 'object' ? (savedRow as Record<string, unknown>)[ROW_OBJECTIVES_KEY] : undefined;

    const adopted = adoptObjectiveIds(currentObjectives as KeyedObjective[], sentObjectives as KeyedObjective[], savedObjectives);
    if (adopted === currentObjectives) return row;
    changed = true;
    return { key: row.key, cells: { ...row.cells, [ROW_OBJECTIVES_KEY]: adopted } };
  });
  return changed ? next : current;
}
