import { createContext } from 'react';
import type { ToastOptions } from './toast-types';

export interface ToastContextValue {
  /** Enqueue a toast (identical concurrent toasts are deduped). */
  show: (options: ToastOptions) => void;
  /** Remove a toast early (also used by the close button). */
  dismiss: (id: number) => void;
}

// Kept in its own module so provider/hook can each import it without tripping
// react-refresh's "only export components" rule.
export const ToastContext = createContext<ToastContextValue | null>(null);
