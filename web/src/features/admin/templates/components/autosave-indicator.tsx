import { Check, CircleAlert } from 'lucide-react';
import { Spinner } from '@/components/ui/spinner';
import type { AutosaveStatus } from '@/features/iep-authoring/hooks/use-autosave';

/** Compact save-state pill for a debounced autosave. Idle renders nothing. */
export function AutosaveIndicator({ status }: { status: AutosaveStatus }) {
  if (status === 'idle') return null;

  if (status === 'saving') {
    return (
      <span className="inline-flex items-center gap-1 text-xs text-brand-slate-400" role="status">
        <Spinner size="sm" tone="current" className="h-3 w-3" aria-hidden="true" />
        Saving…
      </span>
    );
  }

  if (status === 'saved') {
    return (
      <span className="inline-flex items-center gap-1 text-xs text-brand-teal-600" role="status">
        <Check size={12} strokeWidth={2} aria-hidden="true" />
        Saved
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1 text-xs text-brand-danger-700" role="alert">
      <CircleAlert size={12} strokeWidth={2} aria-hidden="true" />
      Save failed
    </span>
  );
}
