import { useCallback, useMemo, useRef, useState } from 'react';
import { apiErrorMessage } from '@/lib/api-error';
import { getDraftExplanations } from '../api/shared-drafts-api';
import type { DraftExplanationDto } from '../types';

export interface UseDraftExplanationsResult {
  /** Start the one shared fetch for this revision (a no-op once it has
   *  started) — call from a card's own "Explain" click so the network request
   *  only fires when a parent actually asks for one. */
  ensureLoaded: () => void;
  isLoading: boolean;
  error: string | null;
  getItemExplanation: (fieldKey: string, rowId: string | null) => string | null;
  /** Section-level explanation: matched by the template section id the server
   *  resolved (`sectionId`), falling back to the title for unresolved ones. */
  getSectionExplanation: (sectionId: number, sectionTitle: string) => string | null;
  disclaimer: string | null;
}

/**
 * The revision's explanations are generated in ONE call covering every
 * section/item (plan6-contract.md) and cached server-side — this hook fetches
 * them once per revision, lazily, the first time any card asks for one. Every
 * card that has clicked "Explain" reads the same shared loading/error/data, so
 * a card only shows a spinner or error when IT has been asked to explain, while
 * the underlying request is deduplicated across the whole page.
 */
export function useDraftExplanations(revisionId: number): UseDraftExplanationsResult {
  const [data, setData] = useState<DraftExplanationDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const startedRef = useRef<number | null>(null);

  // A revision switch invalidates the previous state — computed during render
  // (not an effect; see `use-shared-draft-detail.ts`), so a still-mounted card
  // (same `_rowId` across revisions) never shows the prior revision's cached
  // explanation text. `startedRef` itself needs no reset here: it already
  // guards by revisionId value (below), so a new revisionId alone is enough
  // for `ensureLoaded` to start a fresh fetch — and refs may only be written
  // outside render (its stale-response guard runs inside the fetch's own
  // callbacks, not here).
  const [seenRevisionId, setSeenRevisionId] = useState(revisionId);
  if (revisionId !== seenRevisionId) {
    setSeenRevisionId(revisionId);
    setData(null);
    setIsLoading(false);
    setError(null);
  }

  const ensureLoaded = useCallback(() => {
    if (startedRef.current === revisionId) return;
    startedRef.current = revisionId;
    setIsLoading(true);
    setError(null);
    getDraftExplanations(revisionId)
      .then((res) => {
        if (startedRef.current !== revisionId) return; // superseded by a revision switch
        if (res.success && res.data) setData(res.data);
        else setError(res.message ?? 'Explanations are temporarily unavailable.');
      })
      .catch((err: unknown) => {
        if (startedRef.current !== revisionId) return;
        setError(apiErrorMessage(err, 'Explanations are temporarily unavailable.'));
      })
      .finally(() => {
        if (startedRef.current === revisionId) setIsLoading(false);
      });
  }, [revisionId]);

  const getItemExplanation = useCallback(
    (fieldKey: string, rowId: string | null): string | null => {
      const match = data?.items.find((i) => i.fieldKey === fieldKey && (i.rowId ?? null) === rowId);
      return match?.explanation ?? null;
    },
    [data]
  );

  const getSectionExplanation = useCallback(
    (sectionId: number, sectionTitle: string): string | null => {
      const byId = data?.sections.find((s) => s.sectionId === String(sectionId));
      if (byId) return byId.explanation;
      const wanted = sectionTitle.trim().toLowerCase();
      const byTitle = data?.sections.find((s) => s.title.trim().toLowerCase() === wanted);
      return byTitle?.explanation ?? null;
    },
    [data]
  );

  const disclaimer = data?.disclaimer ?? null;
  return useMemo(
    () => ({ ensureLoaded, isLoading, error, getItemExplanation, getSectionExplanation, disclaimer }),
    [ensureLoaded, isLoading, error, getItemExplanation, getSectionExplanation, disclaimer]
  );
}
