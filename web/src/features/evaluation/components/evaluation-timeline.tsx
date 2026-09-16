import { formatDate } from '@/lib/format-date';
import type { EvaluationTimelineEntryDto } from '../types';

/** Referral → consent requested → consent received → determination due →
 *  determined/closed, each with its date once it has happened. */
export function EvaluationTimeline({ entries }: { entries: EvaluationTimelineEntryDto[] }) {
  if (entries.length === 0) return null;

  return (
    <ol className="space-y-2" data-testid="evaluation-timeline">
      {entries.map((entry, i) => (
        <li key={`${entry.label}-${i}`} className="flex items-center justify-between gap-3 text-sm">
          <span className="text-brand-slate-700">{entry.label}</span>
          <span className="text-brand-slate-500">{formatDate(entry.at)}</span>
        </li>
      ))}
    </ol>
  );
}
