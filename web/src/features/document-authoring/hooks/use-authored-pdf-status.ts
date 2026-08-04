import { useCallback, useEffect, useState } from 'react';
import { usePolling } from '@/hooks/use-polling';
import { getAuthoredPdfStatus, retryAuthoredPdf } from '../api/documents-api';
import type { AuthoredDocumentPdfStatusDto, PdfRenderStatus } from '../types';

interface UseAuthoredPdfStatusResult {
  status: PdfRenderStatus | null;
  errorMessage: string | null;
  isLoading: boolean;
  // True once polling stopped while still Pending (~5-min cap reached).
  timedOut: boolean;
  retry: () => Promise<void>;
  isRetrying: boolean;
  // One-shot status re-fetch (no re-enqueue) + clears the timed-out hint, so a
  // slow-but-still-rendering PDF stays recoverable after polling caps out.
  refresh: () => Promise<void>;
}

// Tracks an authored version's PDF render status. Polls GET …/pdf every 5s while
// Pending, stopping on Rendered/Error (or the usePolling ~5-min cap). `retry`
// re-enqueues an errored/pending render and resumes polling. Mirrors
// iep-versions/use-pdf-status for the authored-document surface.
export function useAuthoredPdfStatus(
  versionId: number,
  // Seed value from the summary/detail so the badge renders before the first fetch.
  initialStatus?: string | null
): UseAuthoredPdfStatusResult {
  const seeded = isRenderStatus(initialStatus) ? initialStatus : null;
  const [pdf, setPdf] = useState<AuthoredDocumentPdfStatusDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [timedOut, setTimedOut] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);

  const status = pdf?.renderStatus ?? seeded;
  // Poll only while Pending/unknown. The download URL is fetched on demand from a
  // separate endpoint, so there is no "Rendered-but-no-URL" transient to wait on.
  const isPending = status === 'Pending' || status === null;

  const fetchStatus = useCallback(async () => {
    try {
      const res = await getAuthoredPdfStatus(versionId);
      if (res.success && res.data) {
        setPdf(res.data);
      }
    } catch {
      // Transient errors keep the last known status; polling will retry.
    }
  }, [versionId]);

  // Initial fetch. isLoading starts true; the effect only setStates after the
  // await (in .then/.finally), keeping it effect-safe.
  useEffect(() => {
    let active = true;
    getAuthoredPdfStatus(versionId)
      .then((res) => {
        if (active && res.success && res.data) setPdf(res.data);
      })
      .catch(() => {
        // handled by interceptor
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [versionId]);

  usePolling(fetchStatus, 5000, isPending && !isLoading);

  // Soft "still generating" hint after ~5 min. Approximate wall-clock timer, not
  // an exact mirror of usePolling's cap (it skips polls while the tab is hidden);
  // this is only a UX nudge, not a hard stop. Only armed while Pending; `retry`
  // resets the flag, and the `timedOut` text only shows in the Pending branch, so
  // a leftover true after Rendered/Error is never rendered.
  useEffect(() => {
    if (!isPending) return;
    const id = setTimeout(() => setTimedOut(true), 5 * 60 * 1000);
    return () => clearTimeout(id);
  }, [isPending]);

  const retry = useCallback(async () => {
    setIsRetrying(true);
    setTimedOut(false);
    try {
      const res = await retryAuthoredPdf(versionId);
      if (res.success && res.data) {
        setPdf(res.data);
      } else {
        await fetchStatus();
      }
    } catch {
      await fetchStatus();
    } finally {
      setIsRetrying(false);
    }
  }, [versionId, fetchStatus]);

  const refresh = useCallback(async () => {
    setTimedOut(false);
    await fetchStatus();
  }, [fetchStatus]);

  return {
    status,
    errorMessage: pdf?.errorMessage ?? null,
    isLoading,
    timedOut,
    retry,
    isRetrying,
    refresh,
  };
}

function isRenderStatus(value: string | null | undefined): value is PdfRenderStatus {
  return value === 'Pending' || value === 'Rendered' || value === 'Error';
}
