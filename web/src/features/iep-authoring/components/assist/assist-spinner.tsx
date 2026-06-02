// Tiny inline spinner sized for buttons/popovers.
export function AssistSpinner({ label = 'Working…' }: { label?: string }) {
  return (
    <span className="inline-flex items-center gap-2 text-[13px] text-brand-slate-500">
      <span
        className="animate-spin rounded-full h-3.5 w-3.5 border-[1.5px] border-brand-teal-300 border-b-transparent"
        aria-hidden="true"
      />
      {label}
    </span>
  );
}
