import { useCallback, useEffect, useRef, useState } from 'react';

export type AutosaveStatus = 'idle' | 'saving' | 'saved' | 'error';

interface UseAutosaveOptions {
  // Debounce window for coalescing rapid edits before firing the save (ms).
  delay?: number;
}

interface UseAutosaveResult<T> {
  status: AutosaveStatus;
  // Schedule a debounced save of `value`. Rapid calls coalesce to the latest value.
  save: (value: T) => void;
  // Immediately run any pending save and await it (used before navigating away).
  flush: () => Promise<void>;
  // Drop the armed debounce timer + any queued value so no further save fires.
  // Used before deleting a row so a pending PUT cannot race the DELETE.
  cancel: () => void;
}

const SAVED_LINGER_MS = 1500;

/**
 * Debounced autosave. `save(value)` coalesces rapid edits to the latest value and
 * fires `saveFn` after `delay`. Status flows idle → saving → saved (→ idle after a
 * short linger) or → error on throw. On error the pending value is kept so the caller
 * can retry simply by calling `save` again. `flush()` runs any pending save now.
 *
 * Refs hold the latest value/fn so a single debounce timer never captures stale data.
 */
export function useAutosave<T>(
  saveFn: (value: T) => Promise<void>,
  opts?: UseAutosaveOptions
): UseAutosaveResult<T> {
  const { delay = 700 } = opts ?? {};

  const [status, setStatus] = useState<AutosaveStatus>('idle');

  const saveFnRef = useRef(saveFn);
  useEffect(() => {
    saveFnRef.current = saveFn;
  }, [saveFn]);

  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const savedTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Holds the latest queued value while a save is pending; null when nothing is queued.
  // Identity matters: each save() creates a fresh object so we can tell a newer
  // queued edit apart from the one we just persisted (see runSave's identity guard).
  const pendingRef = useRef<{ value: T } | null>(null);
  const mountedRef = useRef(true);
  // True while runSave's drain loop is executing — makes runSave non-reentrant.
  const isRunningRef = useRef(false);
  // Set when a save() lands mid-flight so the drain loop runs one more pass.
  const rerunRef = useRef(false);

  const clearTimer = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const clearSavedTimer = useCallback(() => {
    if (savedTimerRef.current) {
      clearTimeout(savedTimerRef.current);
      savedTimerRef.current = null;
    }
  }, []);

  const runSave = useCallback(async () => {
    clearTimer();

    // Non-reentrant: if a drain loop is already running, just flag a rerun (when
    // there is pending work) and return so flush() + the debounce never overlap.
    if (isRunningRef.current) {
      if (pendingRef.current) rerunRef.current = true;
      return;
    }
    if (!pendingRef.current) return;

    isRunningRef.current = true;
    try {
      do {
        rerunRef.current = false;
        const pending: { value: T } | null = pendingRef.current;
        if (!pending) break;

        clearSavedTimer();
        setStatus('saving');
        try {
          await saveFnRef.current(pending.value);
          // Compare by object identity, not value: a newer save() swaps in a
          // fresh { value } object, so this only clears when nothing newer queued.
          if (pendingRef.current === pending) {
            pendingRef.current = null;
          }
          if (mountedRef.current) {
            setStatus('saved');
            savedTimerRef.current = setTimeout(() => {
              if (mountedRef.current) setStatus('idle');
            }, SAVED_LINGER_MS);
          }
        } catch {
          // Keep pendingRef so a subsequent save() (retry) re-sends the value.
          if (mountedRef.current) setStatus('error');
          break;
        }
      } while (rerunRef.current && pendingRef.current);
    } finally {
      isRunningRef.current = false;
    }
  }, [clearTimer, clearSavedTimer]);

  const save = useCallback(
    (value: T) => {
      pendingRef.current = { value };
      clearTimer();
      timerRef.current = setTimeout(() => {
        void runSave();
      }, delay);
    },
    [clearTimer, delay, runSave]
  );

  const flush = useCallback(async () => {
    await runSave();
  }, [runSave]);

  const cancel = useCallback(() => {
    clearTimer();
    pendingRef.current = null;
    rerunRef.current = false;
    clearSavedTimer();
    if (mountedRef.current) setStatus('idle');
  }, [clearTimer, clearSavedTimer]);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (timerRef.current) clearTimeout(timerRef.current);
      if (savedTimerRef.current) clearTimeout(savedTimerRef.current);
    };
  }, []);

  return { status, save, flush, cancel };
}
