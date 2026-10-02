import { useEffect, useLayoutEffect, useRef } from "react";

const MAX_POLLS = 60;
const DEFAULT_INTERVAL = 5000;

// Shared cap for analysis polling (IEP analysis and AnalysisRun, both IEP and
// child-level): 180 polls x 5s = 15 minutes. This is a client-side UX cap,
// not a mirror of the server's timeout — the server fails a run after 30
// minutes of no progress. One constant so the pollers can't drift out of
// sync with each other.
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
    // Flipped in cleanup so an in-flight `await fnRef.current()` can't
    // schedule another round after this effect has been torn down (unmount,
    // or `enabled`/`intervalMs`/`maxPolls` changing) — otherwise that poll
    // loop survives as an orphan alongside whatever replaces it.
    let cancelled = false;

    function scheduleNext() {
      if (cancelled) return;
      if (pollCountRef.current >= maxPolls) return;
      timeoutId = setTimeout(async () => {
        if (cancelled) return;
        if (document.visibilityState === "hidden") {
          scheduleNext();
          return;
        }
        pollCountRef.current += 1;
        await fnRef.current();
        if (cancelled) return;
        scheduleNext();
      }, intervalMs);
    }

    scheduleNext();

    return () => {
      cancelled = true;
      if (timeoutId) clearTimeout(timeoutId);
    };
  }, [enabled, intervalMs, maxPolls]);
}
