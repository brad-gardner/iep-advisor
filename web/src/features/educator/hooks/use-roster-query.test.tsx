import { describe, it, expect } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useRosterQuery } from './use-roster-query';

function wrapper(initial: string) {
  return ({ children }: { children: React.ReactNode }) => (
    <MemoryRouter initialEntries={[initial]}>{children}</MemoryRouter>
  );
}

describe('useRosterQuery', () => {
  it('parses the URL with safe defaults (Active status, page 1, size 50)', () => {
    const { result } = renderHook(() => useRosterQuery(), {
      wrapper: wrapper('/educator/students?status=Bogus&grade=G3&page=x&size=7&school=abc&attention=no-staff'),
    });
    expect(result.current.query).toMatchObject({
      status: 'Active',
      grade: 'G3',
      page: 1,
      pageSize: 50,
      schoolId: null,
      attention: 'NoCaseManager',
    });
  });

  it('maps attention deep-link keys and enum names; ignores unknown values', () => {
    const at = (search: string) =>
      renderHook(() => useRosterQuery(), { wrapper: wrapper(`/educator/students?${search}`) }).result.current
        .query.attention;
    expect(at('attention=no-parent')).toBe('NoLinkedParent');
    expect(at('attention=NoLinkedParent')).toBe('NoLinkedParent');
    expect(at('attention=bogus')).toBeNull();
    expect(at('')).toBeNull();
  });

  it('parses school as a positive integer', () => {
    const { result } = renderHook(() => useRosterQuery(), {
      wrapper: wrapper('/educator/students?school=12'),
    });
    expect(result.current.query.schoolId).toBe(12);
    act(() => result.current.update({ schoolId: null }));
    expect(result.current.query.schoolId).toBeNull();
  });

  it('resets to page 1 when the page size changes', () => {
    const { result } = renderHook(() => useRosterQuery(), {
      wrapper: wrapper('/educator/students?page=4'),
    });
    act(() => result.current.update({ pageSize: 100 }));
    expect(result.current.query).toMatchObject({ page: 1, pageSize: 100 });
  });

  it('ignores a patch that changes nothing (no page reset, no navigation)', () => {
    const { result } = renderHook(() => useRosterQuery(), {
      wrapper: wrapper('/educator/students?q=ada&page=3'),
    });
    const before = result.current.query;
    act(() => result.current.update({ q: ' ada ' }));
    expect(result.current.query).toBe(before);
    expect(result.current.query.page).toBe(3);
  });

  it('resets to page 1 when a filter (not the page) changes', () => {
    const { result } = renderHook(() => useRosterQuery(), {
      wrapper: wrapper('/educator/students?page=3'),
    });
    expect(result.current.query.page).toBe(3);
    act(() => result.current.update({ q: 'ada' }));
    expect(result.current.query).toMatchObject({ q: 'ada', page: 1 });
    act(() => result.current.update({ page: 2 }));
    expect(result.current.query).toMatchObject({ q: 'ada', page: 2 });
  });
});
