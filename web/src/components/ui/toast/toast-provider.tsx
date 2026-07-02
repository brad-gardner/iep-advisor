import { useCallback, useMemo, useRef, useState } from 'react';
import { ToastContext, type ToastContextValue } from './toast-context';
import { ToastViewport } from './toast-viewport';
import type { ToastItem, ToastOptions } from './toast-types';

export const DEFAULT_TOAST_DURATION_MS = 5000;
// Cap concurrent toasts so a burst can't bury the screen; oldest drops first.
export const MAX_VISIBLE_TOASTS = 4;

/**
 * Mounts once at the app root (above the router) and owns the toast queue.
 * `show`/`dismiss` drive the list through functional `setToasts` updaters, so
 * dedupe and the stacking cap are computed against the latest state.
 */
export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const idRef = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const show = useCallback((options: ToastOptions) => {
    const variant = options.variant ?? 'info';
    const { message } = options;
    const id = (idRef.current += 1);

    setToasts((prev) => {
      // Dedupe identical concurrent toasts (same text + variant): if one is
      // already visible, leave the list untouched rather than stacking a copy.
      if (prev.some((t) => t.message === message && t.variant === variant)) {
        return prev;
      }
      const next = [...prev, { id, message, variant }];
      // Cap the stack so a burst can't bury the screen; oldest drops first.
      return next.length > MAX_VISIBLE_TOASTS ? next.slice(next.length - MAX_VISIBLE_TOASTS) : next;
    });
  }, []);

  const value = useMemo<ToastContextValue>(() => ({ show, dismiss }), [show, dismiss]);

  return (
    <ToastContext.Provider value={value}>
      {children}
      <ToastViewport toasts={toasts} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
}
