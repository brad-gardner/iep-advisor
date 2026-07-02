import { createContext } from 'react';
import type { ToastOptions } from './toast-types';

export interface ToastContextValue {
  /** Enqueue a toast; returns its id (or the id of the deduped existing one). */
  show: (options: ToastOptions) => number;
  /** Remove a toast early (also used by the close button). */
  dismiss: (id: number) => void;
}

// Kept in its own module so provider/hook can each import it without tripping
// react-refresh's "only export components" rule.
export const ToastContext = createContext<ToastContextValue | null>(null);
