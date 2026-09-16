import { useCallback, useEffect, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getSharedDraft } from '../api/shared-drafts-api';
import type { SharedDraftRevisionDetailDto } from '../types';

interface UseSharedDraftDetailResult {
  detail: SharedDraftRevisionDetailDto | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
  /** Merge a server response (e.g. after acknowledging) into the loaded detail. */
  applyUpdate: (patch: Partial<SharedDraftRevisionDetailDto>) => void;
}

/** Loads one shared-draft revision's full frozen snapshot for the parent
 *  reading view. Withdrawn/superseded revisions still load — the page renders
 *  their status banner rather than treating them as an error. */
export function useSharedDraftDetail(revisionId: number): UseSharedDraftDetailResult {
  const [detail, setDetail] = useState<SharedDraftRevisionDetailDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  // A revision switch (the revision-switcher `<Link>`) changes `revisionId`
  // without unmounting this hook (same route, new param) — reset synchronously
  // during render so the old revision's frozen values never render alongside
  // the new id. Computed during render, not an effect (see `meeting-drawer.tsx`'s
  // `seenMeetingId` for the same idiom); a bare retry (same revisionId) keeps
  // showing the prior state until the new attempt resolves, like `useHome`.
  const [seenRevisionId, setSeenRevisionId] = useState(revisionId);
  if (revisionId !== seenRevisionId) {
    setSeenRevisionId(revisionId);
    setDetail(null);
    setError(null);
  }

  useEffect(() => {
    if (!revisionId) return;
    let active = true;
    (async () => {
      try {
        const res = await getSharedDraft(revisionId);
        if (!active) return;
        setError(null);
        if (res.success && res.data) setDetail(res.data);
        else setError(res.message ?? 'This shared draft is unavailable.');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'This shared draft is unavailable.'));
      }
    })();
    return () => {
      active = false;
    };
  }, [revisionId, retryToken]);

  const applyUpdate = useCallback((patch: Partial<SharedDraftRevisionDetailDto>) => {
    setDetail((cur) => (cur ? { ...cur, ...patch } : cur));
  }, []);

  return {
    detail,
    isLoading: detail === null && error === null,
    error,
    retry: () => setRetryToken((t) => t + 1),
    applyUpdate,
  };
}
