import { useCallback, useMemo, useRef, useState } from 'react';
import { ToastContext, type ToastContextValue } from './toast-context';
import { ToastViewport } from './toast-viewport';
import type { ToastItem, ToastOptions } from './toast-types';

export const DEFAULT_TOAST_DURATION_MS = 5000;
// Cap concurrent toasts so a burst can't bury the screen; oldest drops first.
export const MAX_VISIBLE_TOASTS = 4;

/**
 * Mounts once at the app root (above the router) and owns the toast queue.
 *
 * `toastsRef` is the source of truth read synchronously by `show`/`dismiss`
 * (both called from event handlers / timers), with `setToasts` mirroring it for
 * render. This lets `show` dedupe against the live list and return a correct id
 * without depending on async state flushes.
 */
export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const toastsRef = useRef<ToastItem[]>([]);
  const idRef = useRef(0);

  const commit = useCallback((next: ToastItem[]) => {
    toastsRef.current = next;
    setToasts(next);
  }, []);

  const dismiss = useCallback(
    (id: number) => {
      commit(toastsRef.current.filter((t) => t.id !== id));
    },
    [commit]
  );

  const show = useCallback(
    (options: ToastOptions): number => {
      const variant = options.variant ?? 'info';
      const durationMs = options.durationMs ?? DEFAULT_TOAST_DURATION_MS;
      const { message } = options;

      // Dedupe identical concurrent messages (same text + variant). Rather than
      // silently drop the re-trigger, bump the existing toast's `seq` so its
      // card re-arms the auto-dismiss timer — the dwell tracks the latest
      // mention (e.g. a second save) instead of expiring on the first.
      const existing = toastsRef.current.find((t) => t.message === message && t.variant === variant);
      if (existing) {
        commit(
          toastsRef.current.map((t) => (t.id === existing.id ? { ...t, seq: t.seq + 1 } : t))
        );
        return existing.id;
      }

      const id = (idRef.current += 1);
      const item: ToastItem = { id, message, variant, durationMs, seq: 0 };
      const next = [...toastsRef.current, item];
      commit(next.length > MAX_VISIBLE_TOASTS ? next.slice(next.length - MAX_VISIBLE_TOASTS) : next);
      return id;
    },
    [commit]
  );

  const value = useMemo<ToastContextValue>(() => ({ show, dismiss }), [show, dismiss]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <ToastViewport toasts={toasts} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
}
