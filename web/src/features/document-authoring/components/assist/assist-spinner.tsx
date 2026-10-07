import i18n from '@/lib/i18n';
import { Spinner } from '@/components/ui/spinner';

// Tiny inline spinner sized for buttons/popovers. `label` is always passed
// explicitly by every current caller (already translated there); the
// default only covers a caller that omits it.
export function AssistSpinner({
  label = i18n.t('document-authoring:assistSpinner.defaultLabel'),
}: {
  label?: string;
}) {
  return (
    <span className="inline-flex items-center gap-2 text-[13px] text-brand-slate-500">
      <Spinner size="sm" tone="current" aria-hidden="true" />
      {label}
    </span>
  );
}
