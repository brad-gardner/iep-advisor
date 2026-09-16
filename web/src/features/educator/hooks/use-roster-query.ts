import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ATTENTION_FILTERS, GRADE_LEVELS, STUDENT_STATUSES } from '../types';
import type { AttentionFilter, GradeLevel, StudentStatusFilter } from '../types';

export const DEFAULT_PAGE_SIZE = 50;
const PAGE_SIZES = new Set([25, 50, 100]);

// The dashboard tiles deep-link with the short keys; the enum names are
// accepted too so a shared link built from the API contract also works.
// A `Map` (rather than a plain object) so a raw value like `"toString"` or
// `"constructor"` can never resolve to an inherited `Object.prototype` member.
const ATTENTION_PARAM_VALUES: Map<string, AttentionFilter> = new Map([
  ['no-staff', 'NoCaseManager'],
  ['no-parent', 'NoLinkedParent'],
  ...ATTENTION_FILTERS.map((f): [string, AttentionFilter] => [f, f]),
]);

// The roster's filter/paging state, kept in the URL so a filtered page is
// shareable and survives back/forward. `attention` is the dashboard deep-link
// (`?attention=no-staff|no-parent`), resolved to the server filter value.
// `from`/`to` (`yyyy-MM-dd`) only apply to `attention: 'DueInRange'` — the
// compliance board's own chosen date window, distinct from the fixed
// `Due30`/`Due60` buckets.
export interface RosterQuery {
  q: string;
  schoolId: number | null;
  status: StudentStatusFilter;
  grade: GradeLevel | '';
  page: number;
  pageSize: number;
  attention: AttentionFilter | null;
  from: string | null;
  to: string | null;
}

export type RosterQueryPatch = Partial<Omit<RosterQuery, 'attention' | 'from' | 'to'>>;

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

function parseAttention(raw: string | null): AttentionFilter | null {
  return raw ? (ATTENTION_PARAM_VALUES.get(raw) ?? null) : null;
}

// `yyyy-MM-dd` only — anything else (including a malformed deep link) is
// treated as absent rather than passed through to the server.
function parseDateParam(raw: string | null): string | null {
  return raw && /^\d{4}-\d{2}-\d{2}$/.test(raw) ? raw : null;
}

export function useRosterQuery(): {
  query: RosterQuery;
  // Merge a patch into the URL. Any *effective* change other than `page`
  // (a filter or the page size) resets to page 1 so a narrower filter or a
  // larger page never lands on an empty page. A no-op patch does nothing.
  update: (patch: RosterQueryPatch) => void;
  clearAttention: () => void;
} {
  const [searchParams, setSearchParams] = useSearchParams();

  const query = useMemo<RosterQuery>(() => {
    const pageSize = parsePositiveInt(searchParams.get('size'), DEFAULT_PAGE_SIZE);
    const schoolId = parsePositiveInt(searchParams.get('school'), 0);
    return {
      q: searchParams.get('q') ?? '',
      schoolId: schoolId > 0 ? schoolId : null,
      status: parseStatus(searchParams.get('status')),
      grade: parseGrade(searchParams.get('grade')),
      page: parsePositiveInt(searchParams.get('page'), 1),
      pageSize: PAGE_SIZES.has(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
      attention: parseAttention(searchParams.get('attention')),
      from: parseDateParam(searchParams.get('from')),
      to: parseDateParam(searchParams.get('to')),
    };
  }, [searchParams]);

  const update = useCallback(
    (patch: RosterQueryPatch) => {
      const next = { ...query, ...patch, q: (patch.q ?? query.q).trim() };
      const changed = (Object.keys(patch) as (keyof RosterQueryPatch)[]).filter(
        (k) => next[k] !== query[k]
      );
      if (changed.length === 0) return;
      if (changed.some((k) => k !== 'page')) next.page = 1;

      const params = new URLSearchParams(searchParams);
      const setOrDelete = (key: string, value: string) =>
        value ? params.set(key, value) : params.delete(key);
      setOrDelete('q', next.q);
      setOrDelete('school', next.schoolId ? String(next.schoolId) : '');
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
    params.delete('from');
    params.delete('to');
    params.delete('page');
    setSearchParams(params, { replace: true });
  }, [searchParams, setSearchParams]);

  return { query, update, clearAttention };
}
