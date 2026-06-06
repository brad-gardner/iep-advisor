import { useCallback, useEffect, useRef, useState } from 'react';
import { usePolling } from '@/hooks/use-polling';
import { getPdfStatus, retryPdf } from '../api/iep-versions-api';
import type { IepVersionPdfStatusDto, PdfRenderStatus } from '../types';

interface UsePdfStatusResult {
  status: PdfRenderStatus | null;
  url: string | null;
  errorMessage: string | null;
  isLoading: boolean;
  // True once polling stopped while still Pending (~5-min cap reached).
  timedOut: boolean;
  retry: () => Promise<void>;
  isRetrying: boolean;
}

// Tracks a version's PDF render status. Polls GET …/pdf every 5s while Pending,
// stopping on Rendered/Error (or the usePolling ~5-min cap). `retry` re-renders
// an errored/pending PDF and resumes polling.
export function usePdfStatus(
  versionId: number,
  // Seed value from the summary/detail so the badge renders before the first fetch.
  initialStatus?: string | null
): UsePdfStatusResult {
  const seeded = isRenderStatus(initialStatus) ? initialStatus : null;
  const [pdf, setPdf] = useState<IepVersionPdfStatusDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [timedOut, setTimedOut] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);

  const status = pdf?.renderStatus ?? seeded;
  // Keep polling while Pending/unknown AND while Rendered-but-url-not-yet-available
  // (a transient where the row flipped to Rendered before the download URL was set) —
  // otherwise the button would show "Generating" forever with no further fetch.
  const renderedWithoutUrl = status === 'Rendered' && pdf != null && !pdf.url;
  const isPending = status === 'Pending' || status === null || renderedWithoutUrl;

  const fetchStatus = useCallback(async () => {
    try {
      const res = await getPdfStatus(versionId);
      if (res.success && res.data) {
        setPdf(res.data);
      }
    } catch {
      // Transient errors keep the last known status; polling will retry.
    }
  }, [versionId]);

  // Initial fetch.
  useEffect(() => {
    let active = true;
    setIsLoading(true);
    getPdfStatus(versionId)
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

  // Soft "still generating" hint after ~5 min. This is an approximate wall-clock
  // timer, not an exact mirror of usePolling's cap: usePolling skips (without
  // counting) polls while the tab is hidden, so on a backgrounded tab the real
  // cap and this timer can drift. It's only a UX nudge, not a hard stop.
  const startedAtRef = useRef<number>(Date.now());
  useEffect(() => {
    if (!isPending) {
      setTimedOut(false);
      startedAtRef.current = Date.now();
      return;
    }
    const id = setTimeout(() => setTimedOut(true), 5 * 60 * 1000);
    return () => clearTimeout(id);
  }, [isPending]);

  const retry = useCallback(async () => {
    setIsRetrying(true);
    setTimedOut(false);
    startedAtRef.current = Date.now();
    try {
      const res = await retryPdf(versionId);
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

  return {
    status,
    url: pdf?.url ?? null,
    errorMessage: pdf?.errorMessage ?? null,
    isLoading,
    timedOut,
    retry,
    isRetrying,
  };
}

function isRenderStatus(value: string | null | undefined): value is PdfRenderStatus {
  return value === 'Pending' || value === 'Rendered' || value === 'Error';
}
