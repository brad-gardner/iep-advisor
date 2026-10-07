import { useCallback, useEffect, useState } from 'react';
import { type LoadError, toLoadError } from '@/lib/api-error';
import { getDraftResponses } from '../api/shared-drafts-api';
import type { DraftResponseDto } from '../types';

interface UseDraftResponsesResult {
  responses: DraftResponseDto[];
  isLoading: boolean;
  error: LoadError | null;
  /** Append a freshly-submitted response to the list. */
  addResponse: (response: DraftResponseDto) => void;
}

/** This parent's own responses (Agree/Question/ChangeRequest/Comment) on a
 *  revision, including any staff reply — one list, read per-card and in the
 *  page's "My responses" section. */
export function useDraftResponses(revisionId: number): UseDraftResponsesResult {
  const [responses, setResponses] = useState<DraftResponseDto[]>([]);
  const [error, setError] = useState<LoadError | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!revisionId) return;
    let active = true;
    (async () => {
      try {
        const res = await getDraftResponses(revisionId);
        if (!active) return;
        if (res.success && res.data) setResponses(res.data);
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
    // `t` deliberately excluded — the generic fallback is a KIND, translated
    // at render time by the sole consumer (`SharedDraftReviewPage`, via
    // `shared-drafts:responsesLoadError`) rather than a string stored here.
  }, [revisionId]);

  const addResponse = useCallback((response: DraftResponseDto) => {
    setResponses((cur) => [response, ...cur]);
  }, []);

  return { responses, isLoading, error, addResponse };
}
