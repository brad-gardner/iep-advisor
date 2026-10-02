import { useCallback, useEffect, useRef, useState } from "react";
import { usePolling, ANALYSIS_MAX_POLLS } from "@/hooks/use-polling";
import { listRuns } from "../api/analysis-runs-api";
import { isTerminalStatus, type AnalysisRun } from "../types";

// Polling caps at 15 minutes (ANALYSIS_MAX_POLLS x 5s). This is a client-side
// UX cap, not a mirror of the server's timeout: the server fails a run after
// 30 minutes of no progress. Past our (shorter) cap we stop spinning and
// surface a "still working" state instead.
const POLL_INTERVAL_MS = 5000;
const POLL_TIMEOUT_MS = ANALYSIS_MAX_POLLS * POLL_INTERVAL_MS;

export function useAnalysisRuns(childId: number) {
  const [runs, setRuns] = useState<AnalysisRun[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [pollingStartedAt, setPollingStartedAt] = useState<number | null>(null);
  const [pollTimedOut, setPollTimedOut] = useState(false);

  // Bumped by reload() and by a childId change, and read back by
  // refreshInBackground once its request settles — a response whose
  // generation has since gone stale (superseded by a newer reload or a
  // childId change) is dropped instead of clobbering fresher state.
  const generationRef = useRef(0);

  // A new child resets the list immediately rather than showing the previous
  // child's runs (or an empty list mistaken for "no runs yet") while the new
  // child's request is in flight.
  useEffect(() => {
    generationRef.current += 1;
    setRuns([]);
    setIsLoading(true);
  }, [childId]);

  const reload = useCallback(async () => {
    if (!childId) return;
    const generation = ++generationRef.current;
    setIsLoading(true);
    // A reload means either the first load for this child, or that a run was
    // just started — either way, any previous "still working" timeout no
    // longer applies, so the in-flight state below gets a fresh window.
    setPollingStartedAt(null);
    setPollTimedOut(false);
    try {
      const res = await listRuns(childId);
      if (generation !== generationRef.current) return;
      if (res.success && res.data) setRuns(res.data);
    } catch {
      // handled by interceptor
    } finally {
      if (generation === generationRef.current) setIsLoading(false);
    }
  }, [childId]);

  useEffect(() => {
    reload();
  }, [reload]);

  const refreshInBackground = useCallback(async () => {
    if (!childId) return;
    const generation = generationRef.current;
    try {
      const res = await listRuns(childId);
      if (generation !== generationRef.current) return;
      if (res.success && res.data) setRuns(res.data);
    } catch {
      // ignore transient polling errors
    }
  }, [childId]);

  const hasInFlight = runs.some((r) => !isTerminalStatus(r.status));

  // Track when an in-flight window began so we can detect the polling cap.
  useEffect(() => {
    if (hasInFlight && pollingStartedAt === null) {
      setPollingStartedAt(Date.now());
      setPollTimedOut(false);
    } else if (!hasInFlight && pollingStartedAt !== null) {
      setPollingStartedAt(null);
      setPollTimedOut(false);
    }
  }, [hasInFlight, pollingStartedAt]);

  // usePolling silently stops at its cap, so we trip the timeout with our own
  // timer to flip the UI into a "still working" state rather than spinning.
  useEffect(() => {
    if (pollingStartedAt === null) return;
    const elapsed = Date.now() - pollingStartedAt;
    const timer = setTimeout(
      () => setPollTimedOut(true),
      Math.max(0, POLL_TIMEOUT_MS - elapsed)
    );
    return () => clearTimeout(timer);
  }, [pollingStartedAt]);

  // Stop polling once we hit the cap; the UI shows a "still working" notice.
  usePolling(
    refreshInBackground,
    POLL_INTERVAL_MS,
    hasInFlight && !pollTimedOut,
    ANALYSIS_MAX_POLLS
  );

  return { runs, isLoading, reload, hasInFlight, pollTimedOut };
}
