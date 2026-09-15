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
      wrapper: wrapper('/educator/students?status=Bogus&grade=G3&page=x&size=7&attention=no-staff'),
    });
    expect(result.current.query).toMatchObject({
      status: 'Active',
      grade: 'G3',
      page: 1,
      pageSize: 50,
      attention: 'no-staff',
    });
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
