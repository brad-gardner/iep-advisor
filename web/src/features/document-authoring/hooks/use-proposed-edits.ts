import { useCallback, useEffect, useState } from 'react';
import { getProposedEdits, markDecisionApplied } from '@/features/meetings/api/meeting-decisions-api';
import type { ProposedEditDto } from '@/features/meetings/types';
import { apiErrorMessage } from '@/lib/api-error';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseProposedEditsError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseProposedEditsResult {
  edits: ProposedEditDto[];
  isLoading: boolean;
  error: UseProposedEditsError | null;
  retry: () => void;
  /** Marks one applied server-side and patches it in place (never removes it —
   *  the panel keeps a full record of every decision surfaced on this draft). */
  markApplied: (decisionId: number) => Promise<void>;
  markingId: number | null;
}

/** Decisions from linked/recent meetings, surfaced as proposed edits on this
 *  draft (plan 7, decision 3) — never auto-applied; a human applies each one
 *  by hand and marks it here once done. */
export function useProposedEdits(instanceId: number): UseProposedEditsResult {
  const [edits, setEdits] = useState<ProposedEditDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<UseProposedEditsError | null>(null);
  const [retryToken, setRetryToken] = useState(0);
  const [markingId, setMarkingId] = useState<number | null>(null);

  useEffect(() => {
    if (!instanceId) return;
    let active = true;
    (async () => {
      try {
        const res = await getProposedEdits(instanceId);
        if (!active) return;
        if (res.success && res.data) {
          setEdits(res.data);
          setError(null);
        } else {
          setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (active) {
          const message = apiErrorMessage(err, '');
          setError(message ? { kind: 'server', message } : { kind: 'generic' });
        }
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [instanceId, retryToken]);

  const markApplied = useCallback(async (decisionId: number) => {
    setMarkingId(decisionId);
    try {
      const res = await markDecisionApplied(decisionId);
      if (res.success && res.data) {
        const updated = res.data;
        setEdits((prev) => prev.map((e) => (e.decisionId === updated.decisionId ? updated : e)));
      }
    } finally {
      setMarkingId(null);
    }
  }, []);

  const retry = useCallback(() => {
    setIsLoading(true);
    setError(null);
    setRetryToken((t) => t + 1);
  }, []);

  return { edits, isLoading, error, retry, markApplied, markingId };
}
