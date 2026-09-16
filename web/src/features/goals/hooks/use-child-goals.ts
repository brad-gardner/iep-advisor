import { useEffect, useState } from 'react';
import { getChildGoals } from '../api/goals-api';
import type { GoalRecordDto } from '../types';

/** Read-only goal records for the parent "Progress" card. Renders nothing
 *  while loading and nothing on failure/empty, matching `SharedDraftsCard`'s
 *  empty-safe idiom — an unlinked child simply shows no progress card. */
export function useChildGoals(childId: number): GoalRecordDto[] | null {
  const [goals, setGoals] = useState<GoalRecordDto[] | null>(null);

  useEffect(() => {
    let active = true;
    getChildGoals(childId)
      .then((res) => {
        if (active) setGoals(res.success && res.data ? res.data : []);
      })
      .catch(() => {
        if (active) setGoals([]);
      });
    return () => {
      active = false;
    };
  }, [childId]);

  return goals;
}
