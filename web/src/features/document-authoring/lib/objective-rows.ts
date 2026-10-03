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

/** Shape guard for the rich, edit-time `KeyedObjective` — as opposed to the
 *  plain wire shape (`{ _rowId?, description, criteria, targetDate }`). A
 *  goal row's `_objectives` cell can legitimately hold either, depending on
 *  whether this editing session has ever touched it (see the module doc
 *  comment above) — every place that reads the cell needs to tell them apart
 *  instead of assuming one or the other. */
export function isKeyedObjective(x: unknown): x is KeyedObjective {
  return (
    !!x &&
    typeof x === 'object' &&
    typeof (x as KeyedObjective).key === 'string' &&
    !!(x as KeyedObjective).cells &&
    typeof (x as KeyedObjective).cells === 'object'
  );
}

/** Objectives from a goal row's `_objectives` cell — either the plain JSON
 *  wire shape (from the server, or an as-yet-untouched row: persisted ones
 *  carry `_rowId`, used as the key, same rule as `coerceRows`) or the rich
 *  `KeyedObjective[]` shape already written by this editor earlier in the same
 *  session (e.g. re-mounting after Done/Edit-goal on an already-edited goal).
 *  An already-keyed item is returned as-is — re-deriving it from `.description`/
 *  `.criteria`/`.targetDate` (which live one level deeper, under `.cells`, on
 *  that shape) would silently blank it out and mint it a fresh key. */
export function coerceObjectives(value: unknown): KeyedObjective[] {
  if (!Array.isArray(value)) return [];
  return value
    .filter((o): o is Record<string, unknown> => typeof o === 'object' && o !== null)
    .map((raw) => {
      if (isKeyedObjective(raw)) return raw;
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

/** Server's own objective count cap (`DocumentInstanceService.MaxObjectives`):
 *  at most this many SURVIVING (kept) objectives are returned. */
const MAX_OBJECTIVES = 20;

/** Mirrors the server's `CoerceObjectives` keep-rule client-side: an objective
 *  reduces to nothing (and is dropped, never assigned an id) when description,
 *  criteria AND targetDate are all blank after trimming; at most the first
 *  `MAX_OBJECTIVES` survivors are kept, in order. `saved` lines up positionally
 *  against exactly this filtered-and-capped list — NOT the raw `sent` order,
 *  which still has every dropped blank still in it. */
function keptByServer(sent: KeyedObjective[]): KeyedObjective[] {
  const kept = sent.filter(
    (o) => o.cells.description.trim() !== '' || o.cells.criteria.trim() !== '' || o.cells.targetDate.trim() !== ''
  );
  return kept.slice(0, MAX_OBJECTIVES);
}

/**
 * Adopts server-assigned objective ids into the objectives that were SENT
 * (matched by client key against the server-KEPT subset of the sent array —
 * see `keptByServer` — index-for-index with the server's response for this
 * row's `_objectives`), never by raw position in `sent` and never by position
 * in `current`. Same invariants as `adoptRowIds`: a key never changes, and an
 * objective that reduced to nothing server-side (blank description/criteria/
 * targetDate) simply gets no id back, same as it never being sent at all —
 * including when it sits BEFORE a surviving objective in `sent`, which would
 * otherwise shift every later pairing off by one.
 */
export function adoptObjectiveIds(current: KeyedObjective[], sent: KeyedObjective[], saved: unknown): KeyedObjective[] {
  if (!Array.isArray(saved)) return current;
  const idByKey = new Map<string, string>();
  keptByServer(sent).forEach((o, i) => {
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
    const currentObjectives = row.cells[ROW_OBJECTIVES_KEY];
    // Only the rich `KeyedObjective[]` shape (minted by this session's own
    // objectives editor) carries a client `.key` to match on — a row whose
    // `_objectives` cell is still the plain wire shape (never opened in the
    // objectives editor this session) has nothing of ours to adopt an id onto.
    if (
      !Array.isArray(sentObjectives) ||
      sentObjectives.length === 0 ||
      !Array.isArray(currentObjectives) ||
      !sentObjectives.every(isKeyedObjective) ||
      !currentObjectives.every(isKeyedObjective)
    )
      return row;

    const savedRow = saved[sentIndex];
    const savedObjectives =
      savedRow && typeof savedRow === 'object' ? (savedRow as Record<string, unknown>)[ROW_OBJECTIVES_KEY] : undefined;

    const adopted = adoptObjectiveIds(currentObjectives, sentObjectives, savedObjectives);
    if (adopted === currentObjectives) return row;
    changed = true;
    return { key: row.key, cells: { ...row.cells, [ROW_OBJECTIVES_KEY]: adopted } };
  });
  return changed ? next : current;
}
