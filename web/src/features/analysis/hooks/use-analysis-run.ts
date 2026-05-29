import { useCallback, useEffect, useState } from "react";
import { usePolling } from "@/hooks/use-polling";
import { getRun } from "../api/analysis-runs-api";
import { isTerminalStatus, type AnalysisRun } from "../types";

const POLL_INTERVAL_MS = 5000;
const POLL_TIMEOUT_MS = 5 * 60 * 1000;

export function useAnalysisRun(childId: number, runId: number | null) {
  const [run, setRun] = useState<AnalysisRun | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [pollingStartedAt, setPollingStartedAt] = useState<number | null>(null);

  const reload = useCallback(async () => {
    if (!childId || runId === null) {
      setRun(null);
      return;
    }
    setIsLoading(true);
    try {
      const res = await getRun(childId, runId);
      if (res.success && res.data) setRun(res.data);
    } catch {
      // handled by interceptor
    } finally {
      setIsLoading(false);
    }
  }, [childId, runId]);

  useEffect(() => {
    reload();
  }, [reload]);

  const refreshInBackground = useCallback(async () => {
    if (!childId || runId === null) return;
    try {
      const res = await getRun(childId, runId);
      if (res.success && res.data) setRun(res.data);
    } catch {
      // ignore transient polling errors
    }
  }, [childId, runId]);

  const inFlight = run !== null && !isTerminalStatus(run.status);
  const [pollTimedOut, setPollTimedOut] = useState(false);

  useEffect(() => {
    if (inFlight && pollingStartedAt === null) {
      setPollingStartedAt(Date.now());
      setPollTimedOut(false);
    } else if (!inFlight && pollingStartedAt !== null) {
      setPollingStartedAt(null);
      setPollTimedOut(false);
    }
  }, [inFlight, pollingStartedAt]);

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

  usePolling(refreshInBackground, POLL_INTERVAL_MS, inFlight && !pollTimedOut);

  return { run, isLoading, reload, pollTimedOut };
}
