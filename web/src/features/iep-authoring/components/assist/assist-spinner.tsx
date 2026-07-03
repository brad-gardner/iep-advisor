import { Spinner } from '@/components/ui/spinner';

// Tiny inline spinner sized for buttons/popovers.
export function AssistSpinner({ label = 'Working…' }: { label?: string }) {
  return (
    <span className="inline-flex items-center gap-2 text-[13px] text-brand-slate-500">
      <Spinner size="sm" tone="current" aria-hidden="true" />
      {label}
    </span>
  );
}
