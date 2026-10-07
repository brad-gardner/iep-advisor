import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
// The singleton api-client reads for Accept-Language.
import appI18n from '@/lib/i18n';
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
  const { i18n } = useTranslation();
  const language = i18n.resolvedLanguage;
  // The server keeps one rendered PDF row per (version, language), and the
  // seed comes from the ENGLISH row on list/detail data — trusting it while
  // a non-English language is active would show an already-"Rendered"
  // download action (with the cached English url) for a language whose row
  // may not even exist yet.
  const seeded = language === 'en' && isRenderStatus(initialStatus) ? initialStatus : null;
  const [pdf, setPdf] = useState<IepVersionPdfStatusDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [timedOut, setTimedOut] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);

  // The server keeps one PDF row per (version, language); `pdf`/`isLoading`
  // cache ONE language's result at a time, and `trackedLanguage` is which
  // language that cache reflects. When the ACTIVE language changes (an
  // in-app switch, no page reload), reset synchronously DURING RENDER —
  // React's documented "adjusting state when a prop changes" pattern, not a
  // useEffect — so a stale Rendered status + url from the PREVIOUS language
  // never flashes on screen (or gets silently downloaded) while the effect
  // below fetches the new language's row.
  const [trackedLanguage, setTrackedLanguage] = useState(language);
  if (language !== trackedLanguage) {
    setTrackedLanguage(language);
    setPdf(null);
    setIsLoading(true);
    setTimedOut(false);
  }

  const status = pdf?.renderStatus ?? seeded;
  // Keep polling while Pending/unknown AND while Rendered-but-url-not-yet-available
  // (a transient where the row flipped to Rendered before the download URL was set) —
  // otherwise the button would show "Generating" forever with no further fetch.
  const renderedWithoutUrl = status === 'Rendered' && pdf != null && !pdf.url;
  const isPending = status === 'Pending' || status === null || renderedWithoutUrl;

  const fetchStatus = useCallback(async () => {
    try {
      // Requests carry the language active when sent (api-client's Accept-Language);
      // drop the answer if the language switched while it was in flight, so a
      // previous language's row never overwrites the reset state.
      const sentWith = appI18n.language;
      const res = await getPdfStatus(versionId);
      if (appI18n.language !== sentWith) return;
      if (res.success && res.data) {
        setPdf(res.data);
      }
    } catch {
      // Transient errors keep the last known status; polling will retry.
    }
  }, [versionId]);

  // Initial fetch, and re-fetch whenever `trackedLanguage` changes — the
  // render-time reset above already cleared the previous language's cached
  // state/url by the time this runs. The server resolves the PDF row from
  // Accept-Language (which api-client sets from `i18n.language`), creating/
  // queuing that language's row on this call if it doesn't exist yet.
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
  }, [versionId, trackedLanguage]);

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
      const sentWith = appI18n.language;
      const res = await retryPdf(versionId);
      if (appI18n.language !== sentWith) return;
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
