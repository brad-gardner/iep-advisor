import { useTranslation } from 'react-i18next';

interface ProgressDotsProps {
  // Zero-based index of the active step.
  current: number;
  total: number;
  // Optional per-step labels; the active label is folded into the aria-label.
  labels?: string[];
  testId?: string;
}

// A small, reusable step indicator. Renders a filled dot per completed/active
// step and exposes its position to assistive tech via role="progressbar".
export function ProgressDots({ current, total, labels, testId }: ProgressDotsProps) {
  const { t } = useTranslation('common');
  const label = labels?.[current];
  // One interpolated key for the accessible name, with a `withLabel` context
  // variant for the step-label case — never English glue text concatenated
  // around a translated label (see `docs/i18n/README.md`'s note on mixed-
  // language sentences).
  const ariaLabel = label
    ? t('ui.stepProgress', { current: current + 1, total, label, context: 'withLabel' })
    : t('ui.stepProgress', { current: current + 1, total });
  return (
    <div
      role="progressbar"
      aria-valuenow={current + 1}
      aria-valuemin={1}
      aria-valuemax={total}
      aria-label={ariaLabel}
      data-testid={testId}
      className="flex items-center justify-center gap-2"
    >
      {Array.from({ length: total }, (_, i) => (
        <div
          key={i}
          className={`w-2 h-2 rounded-full transition-colors ${
            i <= current ? 'bg-brand-teal-500' : 'bg-brand-slate-200'
          }`}
        />
      ))}
    </div>
  );
}
