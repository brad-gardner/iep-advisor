import { useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getShares } from '../api/draft-sharing-api';
import type { SharedDraftRevisionDto } from '../types';

/** A server-provided message is already resolved text; the generic case is translated at render time by the caller. */
export type UseDraftSharesError = { kind: 'server'; message: string } | { kind: 'generic' };

interface UseDraftSharesResult {
  shares: SharedDraftRevisionDto[];
  /** The current Active revision, or `null` if the draft has never been
   *  shared (or every prior share has been withdrawn/superseded with no
   *  successor — i.e. nothing currently Active). */
  latestActive: SharedDraftRevisionDto | null;
  isLoading: boolean;
  error: UseDraftSharesError | null;
}

/** Every revision shared for this document instance, newest first — drives the
 *  staff-side "Shared as revision N" banner under the editor header. */
export function useDraftShares(instanceId: number): UseDraftSharesResult {
  const [shares, setShares] = useState<SharedDraftRevisionDto[]>([]);
  const [error, setError] = useState<UseDraftSharesError | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!instanceId) return;
    let active = true;
    (async () => {
      try {
        const res = await getShares(instanceId);
        if (!active) return;
        if (res.success && res.data) setShares(res.data);
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      } catch (err) {
        if (!active) return;
        const message = apiErrorMessage(err, '');
        setError(message ? { kind: 'server', message } : { kind: 'generic' });
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
