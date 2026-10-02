import { useEffect, useLayoutEffect, useRef } from "react";

const MAX_POLLS = 60;
const DEFAULT_INTERVAL = 5000;

// Shared cap for analysis polling (IEP analysis and AnalysisRun, both IEP and
// child-level): 180 polls x 5s = 15 minutes, matching the server's Claude
// timeout for a full analysis. One constant so the three pollers can't drift
// out of sync with each other or with the server-side timeout again.
export const ANALYSIS_MAX_POLLS = 180;

export function usePolling(
  fn: () => Promise<void>,
  intervalMs: number = DEFAULT_INTERVAL,
  enabled: boolean = false,
  maxPolls: number = MAX_POLLS,
) {
  const pollCountRef = useRef(0);
  const fnRef = useRef(fn);
  useLayoutEffect(() => {
    fnRef.current = fn;
  });

  useEffect(() => {
    if (!enabled) {
      pollCountRef.current = 0;
      return;
    }

    let timeoutId: ReturnType<typeof setTimeout> | null = null;

    function scheduleNext() {
      if (pollCountRef.current >= maxPolls) return;
      timeoutId = setTimeout(async () => {
        if (document.visibilityState === "hidden") {
          scheduleNext();
          return;
        }
        pollCountRef.current += 1;
        await fnRef.current();
        scheduleNext();
      }, intervalMs);
    }

    scheduleNext();

    return () => {
      if (timeoutId) clearTimeout(timeoutId);
    };
  }, [enabled, intervalMs, maxPolls]);
}
