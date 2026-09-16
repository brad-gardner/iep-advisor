import { cn } from '@/lib/cn';

interface SparklineProps {
  /** Used as the SVG's accessible name and the hidden table's caption. */
  title: string;
  values: number[];
  /** Labels for the hidden table's rows, e.g. dates — defaults to 1-based index. */
  pointLabels?: string[];
  className?: string;
  'data-testid'?: string;
}

const WIDTH = 240;
const HEIGHT = 48;
const PADDING = 4;

/**
 * A minimal trend line for a short numeric series (e.g. a metric over the last
 * N days). Inline SVG on the brand teal; `role="img"` + `aria-label` summarize
 * it, and a visually-hidden `<table>` gives the exact per-point values.
 */
export function Sparkline({ title, values, pointLabels, className, 'data-testid': testId }: SparklineProps) {
  const max = Math.max(...values, 0);
  const min = Math.min(...values, 0);
  const range = max - min || 1;

  const points = values.map((v, i) => {
    const x = values.length > 1 ? (i / (values.length - 1)) * (WIDTH - PADDING * 2) + PADDING : WIDTH / 2;
    const y = HEIGHT - PADDING - ((v - min) / range) * (HEIGHT - PADDING * 2);
    return `${x},${y}`;
  });

  return (
    <div className={cn('w-full', className)} data-testid={testId}>
      <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} role="img" aria-label={title} className="w-full">
        {values.length > 0 && (
          <polyline
            points={points.join(' ')}
            fill="none"
            className="stroke-brand-teal-500"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        )}
      </svg>
      <table className="sr-only">
        <caption>{title}</caption>
        <thead>
          <tr>
            <th scope="col">Point</th>
            <th scope="col">Value</th>
          </tr>
        </thead>
        <tbody>
          {values.map((v, i) => (
            <tr key={i}>
              <th scope="row">{pointLabels?.[i] ?? i + 1}</th>
              <td>{v}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
