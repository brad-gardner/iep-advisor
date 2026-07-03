import { useContext } from 'react';
import { ToastContext, type ToastContextValue } from './toast-context';

/**
 * Access the toast API (`show`/`dismiss`). Must be called under a
 * `ToastProvider` (mounted once at the app root).
 */
export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) {
    throw new Error('useToast must be used within a ToastProvider');
  }
  return ctx;
}
