import type { EtrRedFlag } from '../types';
import { EtrRedFlagCard } from './etr-red-flag-card';

interface EtrRedFlagsListProps {
  redFlags: EtrRedFlag[];
}

const SEVERITY_ORDER: Record<string, number> = {
  high: 0,
  medium: 1,
  low: 2,
};

export function EtrRedFlagsList({ redFlags }: EtrRedFlagsListProps) {
  if (redFlags.length === 0) {
    return (
      <div className="text-sm text-brand-slate-400 py-8 text-center">
        No red flags identified.
      </div>
    );
  }

  const sorted = [...redFlags].sort(
    (a, b) => (SEVERITY_ORDER[a.severity] ?? 99) - (SEVERITY_ORDER[b.severity] ?? 99)
  );

  return (
    <div className="space-y-3" data-testid="etr-red-flags-list">
      {sorted.map((flag, i) => (
        <EtrRedFlagCard key={i} redFlag={flag} />
      ))}
    </div>
  );
}
