import { useCallback, useEffect, useState } from "react";
import { usePolling } from "@/hooks/use-polling";
import { listRuns } from "../api/analysis-runs-api";
import { isTerminalStatus, type AnalysisRun } from "../types";

// usePolling caps at ~5 minutes (60 polls x 5s). Past that we stop spinning
// and surface a "still working" state instead.
const POLL_INTERVAL_MS = 5000;
const POLL_TIMEOUT_MS = 5 * 60 * 1000;

export function useAnalysisRuns(childId: number) {
  const [runs, setRuns] = useState<AnalysisRun[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [pollingStartedAt, setPollingStartedAt] = useState<number | null>(null);

  const reload = useCallback(async () => {
    if (!childId) return;
    setIsLoading(true);
    try {
      const res = await listRuns(childId);
      if (res.success && res.data) setRuns(res.data);
    } catch {
      // handled by interceptor
    } finally {
      setIsLoading(false);
    }
  }, [childId]);

  useEffect(() => {
    reload();
  }, [reload]);

  const refreshInBackground = useCallback(async () => {
    if (!childId) return;
    try {
      const res = await listRuns(childId);
      if (res.success && res.data) setRuns(res.data);
    } catch {
      // ignore transient polling errors
    }
  }, [childId]);

  const hasInFlight = runs.some((r) => !isTerminalStatus(r.status));
  const [pollTimedOut, setPollTimedOut] = useState(false);

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

  // usePolling silently stops at its ~5-min cap, so we trip the timeout with our
  // own timer to flip the UI into a "still working" state rather than spinning.
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
  usePolling(refreshInBackground, POLL_INTERVAL_MS, hasInFlight && !pollTimedOut);

  return { runs, isLoading, reload, hasInFlight, pollTimedOut };
}
