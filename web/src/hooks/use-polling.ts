import { useEffect, useLayoutEffect, useRef } from "react";

const MAX_POLLS = 60;
const DEFAULT_INTERVAL = 5000;

export function usePolling(
  fn: () => Promise<void>,
  intervalMs: number = DEFAULT_INTERVAL,
  enabled: boolean = false,
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
      if (pollCountRef.current >= MAX_POLLS) return;
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
  }, [enabled, intervalMs]);
}
