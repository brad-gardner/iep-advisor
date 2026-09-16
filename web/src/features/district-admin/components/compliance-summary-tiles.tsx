import { StatTile } from '@/features/home/components/stat-tile';
import { districtDrillHref } from '../lib/drill-link';
import type { ComplianceSummaryDto } from '../types';

const TILES: {
  key: keyof Omit<ComplianceSummaryDto, 'activeStudents'>;
  label: string;
  tone?: 'warning' | 'danger';
}[] = [
  { key: 'overdueAnnual', label: 'Overdue annual reviews', tone: 'danger' },
  { key: 'overdueReeval', label: 'Overdue reevaluations', tone: 'danger' },
  { key: 'due30', label: 'Due within 30 days', tone: 'warning' },
  { key: 'due60', label: 'Due within 60 days', tone: 'warning' },
  { key: 'unknownDates', label: 'Unknown dates', tone: 'warning' },
  { key: 'noLead', label: 'No case manager' },
];

interface ComplianceSummaryTilesProps {
  summary: ComplianceSummaryDto;
  drill: Record<string, string>;
  /** The currently filtered school (DistrictAdmin only); scopes every tile's link. */
  schoolId: number | null;
}

/** The compliance board's headline tiles — every one links to the roster,
 * pre-filtered by the server's `drill` map (and the current school filter). */
export function ComplianceSummaryTiles({ summary, drill, schoolId }: ComplianceSummaryTilesProps) {
  const denominator = `of ${summary.activeStudents} active students`;
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3" data-testid="compliance-summary-tiles">
      {TILES.map((t) => (
        <StatTile
          key={t.key}
          label={t.label}
          value={summary[t.key]}
          denominator={denominator}
          href={districtDrillHref(drill, t.key, schoolId)}
          tone={t.tone}
          data-testid={`compliance-summary-${t.key}`}
        />
      ))}
    </div>
  );
}
