import { describe, expect, it } from 'vitest';
import {
  adoptObjectiveIds,
  adoptRowObjectiveIds,
  coerceObjectives,
  emptyObjective,
  isKeyedObjective,
  objectiveId,
  toPlainObjectives,
  type KeyedObjective,
} from './objective-rows';
import type { KeyedRow } from './table-rows';

describe('coerceObjectives', () => {
  it('keys persisted objectives by their _rowId and client ones by a temporary key', () => {
    const objectives = coerceObjectives([
      { _rowId: 'OBJ-A', description: 'Write a sentence', criteria: '4/5', targetDate: 'Jan' },
      { description: 'New one' },
      'junk',
      null,
    ]);
    expect(objectives.map((o) => o.key)).toEqual(['OBJ-A', expect.stringMatching(/^objective-\d+$/)]);
    expect(objectiveId(objectives[0])).toBe('OBJ-A');
    expect(objectives[0].cells).toEqual({ _rowId: 'OBJ-A', description: 'Write a sentence', criteria: '4/5', targetDate: 'Jan' });
    expect(objectiveId(objectives[1])).toBeUndefined();
    expect(objectives[1].cells).toEqual({ _rowId: undefined, description: 'New one', criteria: '', targetDate: '' });
  });

  it('returns an empty list for a non-array value', () => {
    expect(coerceObjectives(undefined)).toEqual([]);
    expect(coerceObjectives('nope')).toEqual([]);
  });

  it('returns an already-keyed (rich) objective as-is instead of re-deriving it from the plain shape', () => {
    // Re-mounting the objectives editor (e.g. Done, then Edit goal again) hands
    // `value` back as whatever this editor itself last committed — the rich
    // `KeyedObjective` shape, not the server's plain wire shape. Re-deriving it
    // via `.description`/`.criteria`/`.targetDate` (absent one level up on this
    // shape) would blank the objective out and mint it a fresh key.
    const keyed: KeyedObjective = { key: 'objective-7', cells: { _rowId: 'OBJ-1', description: 'Write a sentence', criteria: '4/5', targetDate: 'Jan' } };
    const objectives = coerceObjectives([keyed]);
    expect(objectives).toEqual([keyed]);
    expect(objectives[0]).toBe(keyed); // same object, not rebuilt
  });
});

describe('isKeyedObjective', () => {
  it('distinguishes the rich shape from the plain wire shape and other values', () => {
    expect(isKeyedObjective({ key: 'objective-1', cells: { description: '', criteria: '', targetDate: '' } })).toBe(true);
    expect(isKeyedObjective({ _rowId: 'OBJ-1', description: 'x', criteria: '', targetDate: '' })).toBe(false);
    expect(isKeyedObjective(null)).toBe(false);
    expect(isKeyedObjective('nope')).toBe(false);
  });
});

describe('emptyObjective', () => {
  it('creates a fresh objective with a unique temporary key and blank fields', () => {
    const a = emptyObjective();
    const b = emptyObjective();
    expect(a.key).not.toBe(b.key);
    expect(a.cells).toEqual({ description: '', criteria: '', targetDate: '' });
  });
});

describe('toPlainObjectives', () => {
  it('flattens to the wire shape, dropping the client key and keeping _rowId only when assigned', () => {
    const objectives: KeyedObjective[] = [
      { key: 'OBJ-A', cells: { _rowId: 'OBJ-A', description: 'a', criteria: 'c', targetDate: 'd' } },
      { key: 'objective-7', cells: { description: 'b', criteria: '', targetDate: '' } },
    ];
    expect(toPlainObjectives(objectives)).toEqual([
      { _rowId: 'OBJ-A', description: 'a', criteria: 'c', targetDate: 'd' },
      { description: 'b', criteria: '', targetDate: '' },
    ]);
  });
});

describe('adoptObjectiveIds', () => {
  const sent: KeyedObjective[] = [
    { key: 'objective-1', cells: { description: 'first', criteria: '', targetDate: '' } },
    { key: 'objective-2', cells: { description: 'second', criteria: '', targetDate: '' } },
  ];
  const saved = [
    { _rowId: 'OBJ-1', description: 'first' },
    { _rowId: 'OBJ-2', description: 'second' },
  ];

  it('adopts ids into the sent objectives without changing their client keys', () => {
    const next = adoptObjectiveIds(sent, sent, saved);
    expect(next.map((o) => o.key)).toEqual(['objective-1', 'objective-2']);
    expect(next.map(objectiveId)).toEqual(['OBJ-1', 'OBJ-2']);
  });

  it('matches by the sent objective, not by position, when one was removed while the save was in flight', () => {
    const current: KeyedObjective[] = [{ key: 'objective-2', cells: { description: 'second edited', criteria: '', targetDate: '' } }];
    const next = adoptObjectiveIds(current, sent, saved);
    expect(next).toHaveLength(1);
    expect(next[0].key).toBe('objective-2');
    expect(objectiveId(next[0])).toBe('OBJ-2'); // not OBJ-1
    expect(next[0].cells.description).toBe('second edited');
  });

  it('leaves objectives added after the request and ones that already have an id untouched', () => {
    const current: KeyedObjective[] = [
      { key: 'objective-1', cells: { _rowId: 'ALREADY', description: 'first', criteria: '', targetDate: '' } },
      { key: 'objective-2', cells: { description: 'second', criteria: '', targetDate: '' } },
      { key: 'objective-3', cells: { description: 'added later', criteria: '', targetDate: '' } },
    ];
    const next = adoptObjectiveIds(current, sent, saved);
    expect(objectiveId(next[0])).toBe('ALREADY');
    expect(objectiveId(next[1])).toBe('OBJ-2');
    expect(objectiveId(next[2])).toBeUndefined();
    expect(next[2]).toBe(current[2]); // same object, no churn
  });

  it('gives no id to an objective the server dropped (blank — reduced to nothing)', () => {
    // A newly-added, still-blank objective sent alongside one real one: the
    // server drops the blank one, so `saved` is shorter than `sent`. Appending
    // only ever happens at the end, so the leading real objective still lines
    // up by position and gets its id.
    const sentWithBlank: KeyedObjective[] = [sent[0], { key: 'objective-9', cells: { description: '', criteria: '', targetDate: '' } }];
    const current: KeyedObjective[] = [...sentWithBlank];
    const next = adoptObjectiveIds(current, sentWithBlank, [{ _rowId: 'OBJ-1', description: 'first' }]);
    expect(objectiveId(next[0])).toBe('OBJ-1');
    expect(objectiveId(next[1])).toBeUndefined();
  });

  it('returns the same array when nothing changes or the response is not an array', () => {
    const current: KeyedObjective[] = [{ key: 'objective-9', cells: { _rowId: 'X', description: 'v', criteria: '', targetDate: '' } }];
    expect(adoptObjectiveIds(current, current, saved)).toBe(current);
    expect(adoptObjectiveIds(current, current, undefined)).toBe(current);
  });

  it('pairs by the server-kept subset, not raw position, when a blank objective precedes a filled one', () => {
    // The server drops the blank (reduced to nothing) before assigning ids, so
    // `saved` is shorter than `sent` AND the blank is not simply trailing —
    // pairing sent[i] <-> saved[i] by raw index would hand the filled
    // objective's id to the blank one instead.
    const blankFirst: KeyedObjective[] = [
      { key: 'objective-1', cells: { description: '', criteria: '', targetDate: '' } },
      { key: 'objective-2', cells: { description: 'first', criteria: '', targetDate: '' } },
    ];
    const next = adoptObjectiveIds(blankFirst, blankFirst, [{ _rowId: 'OBJ-1', description: 'first' }]);
    expect(objectiveId(next[0])).toBeUndefined(); // the blank — never actually kept server-side
    expect(objectiveId(next[1])).toBe('OBJ-1'); // the filled one — correctly paired despite coming second
  });

  it('never pairs beyond the server cap of 20 surviving objectives', () => {
    const sent: KeyedObjective[] = Array.from({ length: 21 }, (_, i) => ({
      key: `objective-${i}`,
      cells: { description: `d${i}`, criteria: '', targetDate: '' },
    }));
    // Only the first 20 are ever assigned ids server-side; the 21st never is.
    const saved = sent.slice(0, 20).map((o, i) => ({ _rowId: `OBJ-${i}`, description: o.cells.description }));
    const next = adoptObjectiveIds(sent, sent, saved);
    expect(objectiveId(next[19])).toBe('OBJ-19');
    expect(objectiveId(next[20])).toBeUndefined();
  });
});

describe('adoptRowObjectiveIds', () => {
  it('adopts objective ids nested inside the matching goal row, leaving other rows and non-array cells untouched', () => {
    const sentObjectives: KeyedObjective[] = [{ key: 'objective-1', cells: { description: 'first', criteria: '', targetDate: '' } }];
    const sentRows: KeyedRow[] = [
      { key: 'ID-1', cells: { goal: 'Read better', _objectives: sentObjectives } },
      { key: 'ID-2', cells: { goal: 'Write better' } }, // no objectives touched
    ];
    const saved = [
      { _rowId: 'ID-1', goal: 'Read better', _objectives: [{ _rowId: 'OBJ-1', description: 'first' }] },
      { _rowId: 'ID-2', goal: 'Write better' },
    ];

    const next = adoptRowObjectiveIds(sentRows, sentRows, saved);
    const objectives = next[0].cells._objectives as KeyedObjective[];
    expect(objectiveId(objectives[0])).toBe('OBJ-1');
    expect(objectives[0].key).toBe('objective-1'); // key unchanged
    expect(next[1]).toBe(sentRows[1]); // untouched — no objectives cell to adopt into
  });

  it('leaves a row whose `_objectives` cell is still the plain wire shape untouched (never opened in the objectives editor this session)', () => {
    // A goal whose text was edited without ever touching its objectives keeps
    // the plain, persisted wire shape — it has no client `.key` to pair ids by,
    // so adoption must skip it rather than try to read `.cells` off a plain entry.
    const plainObjectives = [{ _rowId: 'OBJ-1', description: 'first', criteria: '', targetDate: '' }];
    const sentRows: KeyedRow[] = [{ key: 'ID-1', cells: { goal: 'Read better, updated', _objectives: plainObjectives } }];
    const saved = [{ _rowId: 'ID-1', goal: 'Read better, updated', _objectives: plainObjectives }];

    const next = adoptRowObjectiveIds(sentRows, sentRows, saved);
    expect(next).toBe(sentRows);
    expect(next[0].cells._objectives).toBe(plainObjectives);
  });

  it('skips a row absent from `sent` (added after the request) and returns the same array when nothing changes', () => {
    const current: KeyedRow[] = [{ key: 'NEW', cells: { goal: 'x' } }];
    expect(adoptRowObjectiveIds(current, [], [])).toBe(current);
    expect(adoptRowObjectiveIds(current, current, undefined)).toBe(current);
  });
});
