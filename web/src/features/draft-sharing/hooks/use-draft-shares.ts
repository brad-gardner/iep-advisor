import { useEffect, useState } from 'react';
import { type LoadError, toLoadError } from '@/lib/api-error';
import { getShares } from '../api/draft-sharing-api';
import type { SharedDraftRevisionDto } from '../types';

interface UseDraftSharesResult {
  shares: SharedDraftRevisionDto[];
  /** The current Active revision, or `null` if the draft has never been
   *  shared (or every prior share has been withdrawn/superseded with no
   *  successor — i.e. nothing currently Active). */
  latestActive: SharedDraftRevisionDto | null;
  isLoading: boolean;
  error: LoadError | null;
}

/** Every revision shared for this document instance, newest first — drives the
 *  staff-side "Shared as revision N" banner under the editor header. */
export function useDraftShares(instanceId: number): UseDraftSharesResult {
  const [shares, setShares] = useState<SharedDraftRevisionDto[]>([]);
  const [error, setError] = useState<LoadError | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!instanceId) return;
    let active = true;
    (async () => {
      try {
        const res = await getShares(instanceId);
        if (!active) return;
        if (res.success && res.data) setShares(res.data);
        else setError(toLoadError(res));
      } catch (err) {
        if (!active) return;
        setError(toLoadError(err));
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [instanceId]);

  return {
    shares,
    latestActive: shares.find((s) => s.status === 'Active') ?? null,
    isLoading,
    error,
  };
}
