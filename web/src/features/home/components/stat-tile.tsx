import { Link } from 'react-router-dom';
import { cn } from '@/lib/cn';

type StatTileTone = 'neutral' | 'warning' | 'danger';

interface StatTileProps {
  label: string;
  value: number | string;
  /** Denominator or date-range context, e.g. "of 42 active students". */
  denominator?: string;
  /** Drilldown link. Omit only for a genuinely non-actionable tile (rare) —
   * every tile that CAN link to its underlying rows should. */
  href?: string;
  tone?: StatTileTone;
  'data-testid'?: string;
}

// Tone is a supplementary border cue only — the label text always carries the
// meaning, so this never becomes a colour-only status signal.
const toneBorder: Record<StatTileTone, string> = {
  neutral: 'border-brand-slate-200',
  warning: 'border-brand-amber-200',
  danger: 'border-brand-danger-200',
};

/** A single metric: label, value, optional denominator/date-range, and a
 * drilldown link. The building block for roster-attention, compliance, and
 * adoption/engagement tiles. */
export function StatTile({
  label,
  value,
  denominator,
  href,
  tone = 'neutral',
  'data-testid': testId,
}: StatTileProps) {
  const body = (
    <div
      className={cn('rounded-card border bg-white p-4', toneBorder[tone])}
      data-testid={testId}
    >
      <p className="text-xs font-medium uppercase tracking-wide text-brand-slate-500">{label}</p>
      <p className="mt-1 font-serif text-2xl text-brand-slate-800">{value}</p>
      {denominator && <p className="mt-1 text-xs text-brand-slate-500">{denominator}</p>}
    </div>
  );

  if (!href) return body;

  return (
    <Link
      to={href}
      className="block rounded-card transition-colors hover:border-brand-teal-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
    >
      {body}
    </Link>
  );
}
