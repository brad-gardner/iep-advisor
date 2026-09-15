import { describe, expect, it } from 'vitest';
import { adoptRowIds, carriedFrom, coerceRows, rowId, type KeyedRow } from './table-rows';

const C = 'c1111111-1111-1111-1111-111111111111';

describe('coerceRows', () => {
  it('keys persisted rows by their _rowId and client rows by a temporary key', () => {
    const rows = coerceRows([{ _rowId: 'ID-A', [C]: 'a' }, { [C]: 'b' }, 'junk', null]);
    expect(rows.map((r) => r.key)).toEqual(['ID-A', expect.stringMatching(/^row-\d+$/)]);
    expect(rowId(rows[0])).toBe('ID-A');
    expect(rowId(rows[1])).toBeUndefined();
  });
});

describe('adoptRowIds', () => {
  const sent: KeyedRow[] = [
    { key: 'row-1', cells: { [C]: 'first' } },
    { key: 'row-2', cells: { [C]: 'second' } },
  ];
  const saved = [
    { _rowId: 'ID-1', [C]: 'first' },
    { _rowId: 'ID-2', [C]: 'second' },
  ];

  it('adopts ids into the sent rows without changing their React keys', () => {
    const next = adoptRowIds(sent, sent, saved);
    expect(next.map((r) => r.key)).toEqual(['row-1', 'row-2']);
    expect(next.map(rowId)).toEqual(['ID-1', 'ID-2']);
  });

  it('matches by the sent row, not by position, when a row was removed while the save was in flight', () => {
    const current: KeyedRow[] = [{ key: 'row-2', cells: { [C]: 'second edited' } }];
    const next = adoptRowIds(current, sent, saved);
    expect(next).toHaveLength(1);
    expect(next[0].key).toBe('row-2');
    expect(rowId(next[0])).toBe('ID-2'); // not ID-1
    expect(next[0].cells[C]).toBe('second edited'); // later edit preserved
  });

  it('leaves rows added after the request and rows that already have an id untouched', () => {
    const current: KeyedRow[] = [
      { key: 'row-1', cells: { _rowId: 'ALREADY', [C]: 'first' } },
      { key: 'row-2', cells: { [C]: 'second' } },
      { key: 'row-3', cells: { [C]: 'added later' } },
    ];
    const next = adoptRowIds(current, sent, saved);
    expect(rowId(next[0])).toBe('ALREADY');
    expect(rowId(next[1])).toBe('ID-2');
    expect(rowId(next[2])).toBeUndefined();
    expect(next[2]).toBe(current[2]); // same object, no churn
  });

  it('returns the same array when nothing changes or the response is not an array', () => {
    const current: KeyedRow[] = [{ key: 'row-9', cells: { _rowId: 'X', [C]: 'v' } }];
    expect(adoptRowIds(current, current, saved)).toBe(current);
    expect(adoptRowIds(current, current, undefined)).toBe(current);
  });
});

describe('carriedFrom', () => {
  it('reads well-formed provenance and ignores anything else', () => {
    const ok: KeyedRow = { key: 'a', cells: { _carriedFrom: { versionId: 7, rowId: 'R', label: 'IEP v1', date: '2025-10-14' } } };
    expect(carriedFrom(ok)).toEqual({ versionId: 7, rowId: 'R', label: 'IEP v1', date: '2025-10-14' });
    expect(carriedFrom({ key: 'b', cells: { _carriedFrom: 'junk' } })).toBeUndefined();
    expect(carriedFrom({ key: 'c', cells: {} })).toBeUndefined();
  });
});
