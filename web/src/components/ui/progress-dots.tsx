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
  const label = labels?.[current];
  return (
    <div
      role="progressbar"
      aria-valuenow={current + 1}
      aria-valuemin={1}
      aria-valuemax={total}
      aria-label={
        label
          ? `Step ${current + 1} of ${total}: ${label}`
          : `Step ${current + 1} of ${total}`
      }
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
