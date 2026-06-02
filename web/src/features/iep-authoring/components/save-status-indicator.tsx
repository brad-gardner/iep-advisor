import { Check, CloudOff, Loader2 } from 'lucide-react';
import type { AutosaveStatus } from '../hooks/use-autosave';

interface SaveStatusIndicatorProps {
  status: AutosaveStatus;
}

const config: Record<
  AutosaveStatus,
  { label: string; className: string; Icon: typeof Check | null }
> = {
  idle: { label: 'All changes saved', className: 'text-brand-slate-400', Icon: null },
  saving: { label: 'Saving…', className: 'text-brand-slate-500', Icon: Loader2 },
  saved: { label: 'Saved', className: 'text-brand-teal-600', Icon: Check },
  error: { label: 'Could not save — keep typing to retry', className: 'text-brand-red', Icon: CloudOff },
};

export function SaveStatusIndicator({ status }: SaveStatusIndicatorProps) {
  const { label, className, Icon } = config[status];
  return (
    <p
      className={`flex items-center gap-1.5 text-xs font-medium ${className}`}
      role="status"
      aria-live="polite"
      data-testid="save-status"
    >
      {Icon && (
        <Icon
          className={`w-3.5 h-3.5 ${status === 'saving' ? 'animate-spin' : ''}`}
          strokeWidth={2}
          aria-hidden="true"
        />
      )}
      {label}
    </p>
  );
}
