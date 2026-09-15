import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { GRADE_LEVELS, STUDENT_STATUSES } from '../types';
import type { GradeLevel, StudentStatusFilter } from '../types';

export const DEFAULT_PAGE_SIZE = 50;
const PAGE_SIZES = new Set([25, 50, 100]);

// The roster's filter/paging state, kept in the URL so a filtered page is
// shareable and survives back/forward. `attention` is the dashboard deep-link
// key (`?attention=no-staff|no-parent`).
export interface RosterQuery {
  q: string;
  schoolId: string;
  status: StudentStatusFilter;
  grade: GradeLevel | '';
  page: number;
  pageSize: number;
  attention: string | null;
}

export type RosterQueryPatch = Partial<Omit<RosterQuery, 'attention'>>;

function parseStatus(raw: string | null): StudentStatusFilter {
  if (raw === 'All') return 'All';
  return (STUDENT_STATUSES as readonly string[]).includes(raw ?? '')
    ? (raw as StudentStatusFilter)
    : 'Active';
}

function parseGrade(raw: string | null): GradeLevel | '' {
  return (GRADE_LEVELS as readonly string[]).includes(raw ?? '') ? (raw as GradeLevel) : '';
}

function parsePositiveInt(raw: string | null, fallback: number): number {
  const n = Number(raw);
  return Number.isInteger(n) && n > 0 ? n : fallback;
}

export function useRosterQuery(): {
  query: RosterQuery;
  // Merge a patch into the URL. Any change other than `page`/`pageSize`
  // resets to page 1 so a narrower filter never lands on an empty page.
  update: (patch: RosterQueryPatch) => void;
  clearAttention: () => void;
} {
  const [searchParams, setSearchParams] = useSearchParams();

  const query = useMemo<RosterQuery>(() => {
    const pageSize = parsePositiveInt(searchParams.get('size'), DEFAULT_PAGE_SIZE);
    return {
      q: searchParams.get('q') ?? '',
      schoolId: searchParams.get('school') ?? '',
      status: parseStatus(searchParams.get('status')),
      grade: parseGrade(searchParams.get('grade')),
      page: parsePositiveInt(searchParams.get('page'), 1),
      pageSize: PAGE_SIZES.has(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
      attention: searchParams.get('attention'),
    };
  }, [searchParams]);

  const update = useCallback(
    (patch: RosterQueryPatch) => {
      const next = { ...query, ...patch };
      const filterChanged = Object.keys(patch).some((k) => k !== 'page' && k !== 'pageSize');
      if (filterChanged) next.page = 1;

      const params = new URLSearchParams(searchParams);
      const setOrDelete = (key: string, value: string) =>
        value ? params.set(key, value) : params.delete(key);
      setOrDelete('q', next.q.trim());
      setOrDelete('school', next.schoolId);
      setOrDelete('status', next.status === 'Active' ? '' : next.status);
      setOrDelete('grade', next.grade);
      setOrDelete('page', next.page > 1 ? String(next.page) : '');
      setOrDelete('size', next.pageSize !== DEFAULT_PAGE_SIZE ? String(next.pageSize) : '');
      setSearchParams(params, { replace: true });
    },
    [query, searchParams, setSearchParams]
  );

  const clearAttention = useCallback(() => {
    const params = new URLSearchParams(searchParams);
    params.delete('attention');
    setSearchParams(params, { replace: true });
  }, [searchParams, setSearchParams]);

  return { query, update, clearAttention };
}
