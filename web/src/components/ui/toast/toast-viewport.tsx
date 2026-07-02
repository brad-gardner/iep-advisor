import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { CheckCircle, Info, XCircle, X } from 'lucide-react';
import { cn } from '@/lib/cn';
import type { ToastItem, ToastVariant } from './toast-types';

const variantStyles: Record<ToastVariant, { bg: string; border: string; text: string; Icon: typeof Info }> = {
  success: { bg: 'bg-brand-teal-50', border: 'border-brand-teal-100', text: 'text-brand-teal-600', Icon: CheckCircle },
  error: { bg: 'bg-brand-danger-50', border: 'border-brand-danger-200', text: 'text-brand-danger-700', Icon: XCircle },
  info: { bg: 'bg-brand-slate-50', border: 'border-brand-slate-200', text: 'text-brand-slate-700', Icon: Info },
};

function ToastCard({ toast, onDismiss }: { toast: ToastItem; onDismiss: (id: number) => void }) {
  const { bg, border, text, Icon } = variantStyles[toast.variant];
  // Pause auto-dismiss while hovered so a reader isn't rushed. Toggling this in
  // the pointer handlers restarts the effect's timer below.
  const [paused, setPaused] = useState(false);

  useEffect(() => {
    if (paused) return;
    const timer = setTimeout(() => onDismiss(toast.id), toast.durationMs);
    return () => clearTimeout(timer);
    // `toast.seq` bumps on a deduped re-show, re-arming the timer so the dwell
    // restarts from the latest mention.
  }, [paused, toast.id, toast.durationMs, toast.seq, onDismiss]);

  return (
    <div
      data-testid="toast"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      className={cn(
        'pointer-events-auto flex items-start gap-3 rounded-card border p-4 shadow-sm',
        // Entrance is gated behind motion-safe so it never plays under
        // prefers-reduced-motion.
        'motion-safe:animate-toast-in',
        bg,
        border
      )}
    >
      <Icon className={cn('mt-0.5 h-5 w-5 shrink-0', text)} strokeWidth={1.8} aria-hidden="true" />
      <p className={cn('flex-1 text-sm font-medium', text)}>{toast.message}</p>
      <button
        type="button"
        onClick={() => onDismiss(toast.id)}
        aria-label="Dismiss notification"
        className={cn('-m-1 shrink-0 rounded p-1 transition-colors hover:bg-black/5', text)}
      >
        <X className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
      </button>
    </div>
  );
}

/**
 * Fixed, bottom-right stack portaled to `document.body`. The container is a
 * polite live region (`role="status"` + `aria-live="polite"`) so new toasts are
 * announced without stealing focus. `pointer-events-none` lets clicks pass
 * through the empty gaps; each card re-enables pointer events.
 */
export function ToastViewport({ toasts, onDismiss }: { toasts: ToastItem[]; onDismiss: (id: number) => void }) {
  return createPortal(
    <div
      role="status"
      aria-live="polite"
      aria-atomic="false"
      className="pointer-events-none fixed bottom-4 right-4 z-50 flex w-full max-w-sm flex-col gap-2"
    >
      {toasts.map((toast) => (
        <ToastCard key={toast.id} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>,
    document.body
  );
}
