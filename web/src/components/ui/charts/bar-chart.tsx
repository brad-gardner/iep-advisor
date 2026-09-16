import { cn } from '@/lib/cn';

export interface BarChartDatum {
  label: string;
  value: number;
}

interface BarChartProps {
  /** Used as the SVG's accessible name and the hidden table's caption. */
  title: string;
  data: BarChartDatum[];
  /** Scale ceiling; defaults to the largest value in `data` (minimum 1). */
  max?: number;
  /** Formats the value shown beside each bar and in the table fallback. */
  valueFormat?: (value: number) => string;
  className?: string;
  'data-testid'?: string;
}

const ROW_HEIGHT = 28;
const CHART_WIDTH = 320;
const LABEL_WIDTH = 104;
const VALUE_WIDTH = 40;
const BAR_AREA = CHART_WIDTH - LABEL_WIDTH - VALUE_WIDTH;

function truncateLabel(label: string): string {
  return label.length > 15 ? `${label.slice(0, 14)}…` : label;
}

/**
 * A horizontal bar chart, one row per datum. Inline SVG on the brand palette —
 * no chart library. `role="img"` + `aria-label` give a one-line summary; the
 * visually-hidden `<table>` gives assistive tech (and anyone else) the exact
 * numbers the bars only approximate visually.
 */
export function BarChart({
  title,
  data,
  max,
  valueFormat = (v) => String(v),
  className,
  'data-testid': testId,
}: BarChartProps) {
  const maxValue = Math.max(max ?? 0, ...data.map((d) => d.value), 1);
  const height = data.length * ROW_HEIGHT + 8;

  return (
    <div className={cn('w-full', className)} data-testid={testId}>
      <svg viewBox={`0 0 ${CHART_WIDTH} ${height}`} role="img" aria-label={title} className="w-full">
        {data.map((d, i) => {
          const y = i * ROW_HEIGHT + 4;
          const barHeight = ROW_HEIGHT - 12;
          const barWidth = maxValue > 0 ? (d.value / maxValue) * BAR_AREA : 0;
          return (
            <g key={d.label}>
              <text x={0} y={y + barHeight} className="fill-brand-slate-600 text-[9px]">
                {truncateLabel(d.label)}
              </text>
              <rect
                x={LABEL_WIDTH}
                y={y}
                width={BAR_AREA}
                height={barHeight}
                rx={3}
                className="fill-brand-slate-100"
              />
              <rect
                x={LABEL_WIDTH}
                y={y}
                width={Math.max(barWidth, d.value > 0 ? 2 : 0)}
                height={barHeight}
                rx={3}
                className="fill-brand-teal-500"
              />
              <text
                x={LABEL_WIDTH + BAR_AREA + 6}
                y={y + barHeight}
                className="fill-brand-slate-700 text-[9px] font-medium"
              >
                {valueFormat(d.value)}
              </text>
            </g>
          );
        })}
      </svg>
      <table className="sr-only">
        <caption>{title}</caption>
        <thead>
          <tr>
            <th scope="col">Label</th>
            <th scope="col">Value</th>
          </tr>
        </thead>
        <tbody>
          {data.map((d) => (
            <tr key={d.label}>
              <th scope="row">{d.label}</th>
              <td>{valueFormat(d.value)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
