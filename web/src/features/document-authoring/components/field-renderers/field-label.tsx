/** Field label with a required marker. Announced to screen readers via sr-only
 *  text (validation itself is enforced at finalize in Phase 4, not here). */
export function FieldLabel({
  htmlFor,
  label,
  required,
}: {
  htmlFor: string;
  label: string;
  required: boolean;
}) {
  return (
    <label htmlFor={htmlFor} className="mb-1 block text-[13px] font-medium text-brand-slate-600">
      {label || 'Untitled field'}
      {required && (
        <span className="ml-1 text-brand-danger-700" aria-hidden="true">
          *
        </span>
      )}
      {required && <span className="sr-only"> (required)</span>}
    </label>
  );
}
