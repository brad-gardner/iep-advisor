import { cn } from '@/lib/cn';

export interface StackedBarSegment {
  key: string;
  label: string;
  value: number;
}

export interface StackedBarRow {
  label: string;
  segments: StackedBarSegment[];
}

interface StackedBarChartProps {
  title: string;
  rows: StackedBarRow[];
  className?: string;
  'data-testid'?: string;
}

const ROW_HEIGHT = 28;
const CHART_WIDTH = 320;
const LABEL_WIDTH = 104;
const BAR_AREA = CHART_WIDTH - LABEL_WIDTH - 16;

// A fixed, brand-palette order so a segment key always gets the same colour
// across rows (and across renders) — legend order follows the first row.
const SEGMENT_FILL_CLASSES = [
  'fill-brand-danger-500',
  'fill-brand-amber-500',
  'fill-brand-teal-500',
  'fill-brand-slate-500',
  'fill-brand-slate-300',
];
const SEGMENT_SWATCH_CLASSES = [
  'bg-brand-danger-500',
  'bg-brand-amber-500',
  'bg-brand-teal-500',
  'bg-brand-slate-500',
  'bg-brand-slate-300',
];

function truncateLabel(label: string): string {
  return label.length > 15 ? `${label.slice(0, 14)}…` : label;
}

function rowTotal(row: StackedBarRow): number {
  return row.segments.reduce((sum, s) => sum + s.value, 0);
}

/**
 * A horizontal stacked-bar chart, one row per category (e.g. school), each
 * bar divided into the same ordered set of segments (e.g. deadline buckets).
 * Inline SVG + a text legend (never colour-only) + a visually-hidden table
 * with the full per-segment breakdown.
 */
export function StackedBarChart({ title, rows, className, 'data-testid': testId }: StackedBarChartProps) {
  const maxTotal = Math.max(...rows.map(rowTotal), 1);
  const height = rows.length * ROW_HEIGHT + 8;
  const legendSegments = rows[0]?.segments ?? [];

  return (
    <div className={cn('w-full', className)} data-testid={testId}>
      <svg viewBox={`0 0 ${CHART_WIDTH} ${height}`} role="img" aria-label={title} className="w-full">
        {rows.map((row, rowIndex) => {
          const y = rowIndex * ROW_HEIGHT + 4;
          const barHeight = ROW_HEIGHT - 12;
          let x = LABEL_WIDTH;
          return (
            <g key={row.label}>
              <text x={0} y={y + barHeight} className="fill-brand-slate-600 text-[9px]">
                {truncateLabel(row.label)}
              </text>
              <rect x={LABEL_WIDTH} y={y} width={BAR_AREA} height={barHeight} rx={3} className="fill-brand-slate-100" />
              {row.segments.map((segment, segmentIndex) => {
                const width = maxTotal > 0 ? (segment.value / maxTotal) * BAR_AREA : 0;
                const rect = (
                  <rect
                    key={segment.key}
                    x={x}
                    y={y}
                    width={width}
                    height={barHeight}
                    className={SEGMENT_FILL_CLASSES[segmentIndex % SEGMENT_FILL_CLASSES.length]}
                  />
                );
                x += width;
                return rect;
              })}
            </g>
          );
        })}
      </svg>

      {legendSegments.length > 0 && (
        <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-brand-slate-600">
          {legendSegments.map((segment, i) => (
            <li key={segment.key} className="flex items-center gap-1.5">
              <span
                aria-hidden="true"
                className={cn('h-2.5 w-2.5 rounded-sm', SEGMENT_SWATCH_CLASSES[i % SEGMENT_SWATCH_CLASSES.length])}
              />
              {segment.label}
            </li>
          ))}
        </ul>
      )}

      <table className="sr-only">
        <caption>{title}</caption>
        <thead>
          <tr>
            <th scope="col">Label</th>
            {legendSegments.map((segment) => (
              <th scope="col" key={segment.key}>
                {segment.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.label}>
              <th scope="row">{row.label}</th>
              {row.segments.map((segment) => (
                <td key={segment.key}>{segment.value}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
