import { useCallback, useEffect, useRef, useState } from 'react';
import type { EtrDocument } from '../types';
import { getById } from '../api/etr-documents-api';

const POLL_INTERVAL_MS = 3000;
const MAX_POLLS = 120; // ~6 minutes

const IN_FLIGHT_STATUSES = new Set(['uploaded', 'processing']);

interface UseEtrProcessingResult {
  etr: EtrDocument | null;
  status: string | null;
  isPolling: boolean;
  refresh: () => Promise<void>;
}

/**
 * Polls the ETR document while it is in an in-flight status
 * (uploaded or processing). Stops automatically when status
 * reaches `parsed` or `error`.
 */
export function useEtrProcessing(
  etrId: number,
  initialEtr: EtrDocument | null,
  onStatusChange?: (etr: EtrDocument) => void
): UseEtrProcessingResult {
  const [etr, setEtr] = useState<EtrDocument | null>(initialEtr);
  const [isPolling, setIsPolling] = useState(false);
  const pollCountRef = useRef(0);
  const onStatusChangeRef = useRef(onStatusChange);

  useEffect(() => {
    onStatusChangeRef.current = onStatusChange;
  });

  useEffect(() => {
    setEtr(initialEtr);
  }, [initialEtr]);

  const refresh = useCallback(async () => {
    try {
      const response = await getById(etrId);
      if (response.success && response.data) {
        setEtr(response.data);
        onStatusChangeRef.current?.(response.data);
      }
    } catch {
      // swallow; caller can retry
    }
  }, [etrId]);

  useEffect(() => {
    const currentStatus = etr?.status;
    if (!currentStatus || !IN_FLIGHT_STATUSES.has(currentStatus)) {
      setIsPolling(false);
      pollCountRef.current = 0;
      return;
    }

    setIsPolling(true);
    let cancelled = false;
    let timeoutId: ReturnType<typeof setTimeout> | null = null;

    async function tick() {
      if (cancelled) return;
      if (pollCountRef.current >= MAX_POLLS) {
        setIsPolling(false);
        return;
      }
      if (document.visibilityState === 'hidden') {
        timeoutId = setTimeout(tick, POLL_INTERVAL_MS);
        return;
      }
      pollCountRef.current += 1;
      try {
        const response = await getById(etrId);
        if (cancelled) return;
        if (response.success && response.data) {
          setEtr(response.data);
          onStatusChangeRef.current?.(response.data);
          if (!IN_FLIGHT_STATUSES.has(response.data.status)) {
            setIsPolling(false);
            return;
          }
        }
      } catch {
        // keep polling on transient failure
      }
      timeoutId = setTimeout(tick, POLL_INTERVAL_MS);
    }

    timeoutId = setTimeout(tick, POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      if (timeoutId) clearTimeout(timeoutId);
    };
  }, [etr?.status, etrId]);

  return {
    etr,
    status: etr?.status ?? null,
    isPolling,
    refresh,
  };
}
