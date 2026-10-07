import { useCallback, useEffect, useState } from 'react';
import { type LoadError, toLoadError } from '@/lib/api-error';
import { getSharedDraft } from '../api/shared-drafts-api';
import type { SharedDraftRevisionDetailDto } from '../types';

interface UseSharedDraftDetailResult {
  detail: SharedDraftRevisionDetailDto | null;
  isLoading: boolean;
  error: LoadError | null;
  retry: () => void;
  /** Merge a server response (e.g. after acknowledging) into the loaded detail. */
  applyUpdate: (patch: Partial<SharedDraftRevisionDetailDto>) => void;
}

/** Loads one shared-draft revision's full frozen snapshot for the parent
 *  reading view. Withdrawn/superseded revisions still load — the page renders
 *  their status banner rather than treating them as an error. */
export function useSharedDraftDetail(revisionId: number): UseSharedDraftDetailResult {
  const [detail, setDetail] = useState<SharedDraftRevisionDetailDto | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
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
        else setError(toLoadError(res));
      } catch (err) {
        if (!active) return;
        setError(toLoadError(err));
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — a server-provided message is already
    // resolved text (localized server-side); the generic fallback is
    // translated at RENDER time by the sole consumer (`SharedDraftReviewPage`,
    // via `shared-drafts:reviewPage.unavailableDefault`) instead of here, so a
    // language switch after a failed load shows the new language immediately,
    // without needing to refetch — same idiom as `useHome`.
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
