import { useTranslation } from 'react-i18next';
import { StatTile } from '@/features/home/components/stat-tile';
import { formatDate } from '@/lib/format-date';
import { complianceTileLabel } from '@/lib/compliance-tile-label';
import { COMPLIANCE_SUMMARY_TILES } from '../lib/compliance-tiles';
import { districtDrillHref } from '../lib/drill-link';
import type { ComplianceSummaryDto } from '../types';

interface ComplianceSummaryTilesProps {
  summary: ComplianceSummaryDto;
  drill: Record<string, string>;
  /** The currently filtered school (DistrictAdmin only); scopes every tile's link. */
  schoolId: number | null;
  /** The board's chosen due-date window — labels the `dueInRange` tile only
   * the board (not the home teaser) shows, since only the board has a range
   * picker for it to describe. */
  from: string;
  to: string;
}

/** The compliance board's headline tiles — every one links to the roster,
 * pre-filtered by the server's `drill` map (and the current school filter). */
export function ComplianceSummaryTiles({ summary, drill, schoolId, from, to }: ComplianceSummaryTilesProps) {
  const { t } = useTranslation('district-admin');
  const denominator = t('complianceTiles.denominator', { count: summary.activeStudents });
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3" data-testid="compliance-summary-tiles">
      {COMPLIANCE_SUMMARY_TILES.map((tile) => (
        <StatTile
          key={tile.key}
          label={complianceTileLabel(tile.key)}
          value={summary[tile.key]}
          denominator={denominator}
          href={districtDrillHref(drill, tile.key, schoolId)}
          tone={tile.tone}
          data-testid={`compliance-summary-${tile.key}`}
        />
      ))}
      <StatTile
        label={t('complianceTiles.dueInRange')}
        value={summary.dueInRange}
        denominator={`${formatDate(from)} – ${formatDate(to)}`}
        href={districtDrillHref(drill, 'dueInRange', schoolId)}
        tone="warning"
        data-testid="compliance-summary-dueInRange"
      />
    </div>
  );
}
