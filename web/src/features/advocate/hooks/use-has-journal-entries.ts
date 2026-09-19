import { useEffect, useState } from 'react';
import { listJournalEntries } from '@/features/journal/api/journal-api';

/**
 * Whether the child has any journal entries — one cheap `take: 1` call, used
 * to decide if the "raise from my journal" example question makes sense.
 * Unknown (null) while loading or when the call fails, which reads as "no".
 */
export function useHasJournalEntries(childId: number): boolean | null {
  const [has, setHas] = useState<boolean | null>(null);
  useEffect(() => {
    let active = true;
    listJournalEntries(childId, { take: 1 })
      .then((res) => {
        if (active) setHas(Boolean(res.success && res.data && res.data.length > 0));
      })
      .catch(() => {
        if (active) setHas(false);
      });
    return () => {
      active = false;
    };
  }, [childId]);
  return has;
}
