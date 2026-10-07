import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getConverge } from '../api/draft-sharing-api';
import type { ConvergeDto } from '../types';

// See `@/features/shared-drafts/hooks/use-shared-draft-detail`'s
// `SharedDraftDetailError` for why the generic fallback is a KIND, translated
// at render time by the sole consumer (`ConvergePanel`, via
// `draft-sharing:converge.loadErrorDefault`) rather than a string stored here.
export type ConvergeLoadError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseConvergeResult {
  converge: ConvergeDto | null;
  isLoading: boolean;
  error: ConvergeLoadError | null;
  retry: () => void;
  /** Merge a response-level update (e.g. a resolve) into the loaded lists. */
  applyResolvedResponse: (updated: ConvergeDto['openResponses'][number]) => void;
}

/** The staff Converge tab's aggregate: latest revision, open/resolved
 *  responses, and the live-draft-vs-latest-share diff. */
export function useConverge(instanceId: number): UseConvergeResult {
  const [converge, setConverge] = useState<ConvergeDto | null>(null);
  const [error, setError] = useState<ConvergeLoadError | null>(null);
  const [retryToken, setRetryToken] = useState(0);
  const [loadedFor, setLoadedFor] = useState<number | null>(null);

  useEffect(() => {
    if (!instanceId) return;
    let active = true;
    (async () => {
      // A refresh (e.g. after "Share again") keeps the current data on screen while it
      // reloads, so an open reply/resolve dialog — and the reply typed into it — survives.
      // Only a genuine instance switch drops the previous document's data.
      setError(null);
      try {
        const res = await getConverge(instanceId);
        if (!active) return;
        if (res.success && res.data) {
          setConverge(res.data);
          setLoadedFor(instanceId);
        } else {
          setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see `ConvergeLoadError` above.
  }, [instanceId, retryToken]);

  const current = loadedFor === instanceId ? converge : null;

  const applyResolvedResponse = useCallback((updated: ConvergeDto['openResponses'][number]) => {
    setConverge((cur) => {
      if (!cur) return cur;
      return {
        ...cur,
        openResponses: cur.openResponses.filter((r) => r.id !== updated.id),
        resolvedResponses: [updated, ...cur.resolvedResponses],
      };
    });
  }, []);

  return {
    converge: current,
    isLoading: current === null && error === null,
    error,
    retry: () => setRetryToken((n) => n + 1),
    applyResolvedResponse,
  };
}
