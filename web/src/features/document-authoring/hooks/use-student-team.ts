import { useEffect, useState } from 'react';
import { getTeam } from '@/features/educator/api/educator-api';
import type { StudentTeamMember } from '@/features/educator/types';

type LoadState = 'loading' | 'ready' | 'error';

export interface StudentTeamCache {
  /** Every member returned by the server (active and inactive) — callers that
   *  need "currently assignable" filter on `isActive` themselves. */
  members: StudentTeamMember[];
  isLoading: boolean;
  isError: boolean;
}

/**
 * Loads the student's IEP team once per document editor session (plan
 * 2026-10-02-002: item owners). Unlike `useStudentShareableEntries` (lazy,
 * fetched only when a "Pull from student" picker opens), the team is needed as
 * soon as any section renders — read views resolve an owner's name from it, and
 * row editors populate the owner picker from it — so this fetches eagerly on
 * mount. One instance lives in the editor context so every owner picker and
 * read view in a document shares the same fetch and cache.
 */
export function useStudentTeam(studentId: number): StudentTeamCache {
  const [members, setMembers] = useState<StudentTeamMember[]>([]);
  const [state, setState] = useState<LoadState>('loading');
  // Tracks which student the current `members`/`state` belong to. A `studentId`
  // change resets to loading DURING RENDER (React's "adjust state while
  // rendering" pattern — react.dev/learn/you-might-not-need-an-effect#adjusting-
  // some-state-when-a-prop-changes) rather than via a setState call at the top of
  // the effect below, which would fire AFTER a render with the previous (now
  // stale) student's members still showing.
  const [loadedForId, setLoadedForId] = useState(studentId);
  if (studentId !== loadedForId) {
    setLoadedForId(studentId);
    setState('loading');
    setMembers([]);
  }

  useEffect(() => {
    let cancelled = false;
    getTeam(studentId)
      .then((res) => {
        if (cancelled) return;
        if (res.success && res.data) {
          setMembers(res.data);
          setState('ready');
        } else {
          setState('error');
        }
      })
      .catch(() => {
        if (!cancelled) setState('error');
      });
    return () => {
      cancelled = true;
    };
  }, [studentId]);

  return { members, isLoading: state === 'loading', isError: state === 'error' };
}
