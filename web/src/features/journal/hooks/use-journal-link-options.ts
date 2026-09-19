import { useEffect, useState } from 'react';
import { getIepDocuments } from '@/features/iep-documents/api/iep-documents-api';
import { listByChild as listEtrsByChild } from '@/features/etr-documents/api/etr-documents-api';
import { listChildMeetings } from '@/features/meetings/api/meetings-api';
import { formatDate } from '@/lib/format-date';

export interface JournalLinkOption {
  id: number;
  label: string;
}

export interface JournalLinkOptions {
  ieps: JournalLinkOption[];
  etrs: JournalLinkOption[];
  /** Empty for a child with no school link — the meetings endpoint has nothing to offer then. */
  meetings: JournalLinkOption[];
}

const EMPTY: JournalLinkOptions = { ieps: [], etrs: [], meetings: [] };

/**
 * The child's IEPs, ETRs and (school-linked only) meetings, as `<select>`
 * options for an entry's optional "Link to" fields. Loaded once per form
 * mount; each list fails independently and quietly — a link selector that
 * cannot be populated is simply not offered, the entry itself is unaffected.
 */
export function useJournalLinkOptions(childId: number): { options: JournalLinkOptions; loading: boolean } {
  const [options, setOptions] = useState<JournalLinkOptions>(EMPTY);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    const quiet = <T>(p: Promise<{ success: boolean; data?: T | null }>): Promise<T | null> =>
      p.then((res) => (res.success && res.data ? res.data : null)).catch(() => null);

    Promise.all([quiet(getIepDocuments(childId)), quiet(listEtrsByChild(childId)), quiet(listChildMeetings(childId))]).then(
      ([ieps, etrs, meetings]) => {
        if (!active) return;
        setOptions({
          ieps: (ieps ?? []).map((d) => ({ id: d.id, label: `IEP · ${formatDate(d.iepDate, d.fileName)}` })),
          etrs: (etrs ?? []).map((d) => ({
            id: d.id,
            label: `${d.evaluationType || 'ETR'} · ${formatDate(d.evaluationDate, d.fileName ?? 'undated')}`,
          })),
          meetings: (meetings ?? []).map((m) => ({ id: m.id, label: `${m.title} · ${formatDate(m.startsAtUtc)}` })),
        });
        setLoading(false);
      },
    );
    return () => {
      active = false;
    };
  }, [childId]);

  return { options, loading };
}
