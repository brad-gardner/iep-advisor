import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { COMPLIANCE_SUMMARY_TILES } from '@/features/district-admin/lib/compliance-tiles';
import type { ComplianceSummaryDto } from '@/features/district-admin/types';
import { HomeSection } from './home-section';
import { StatTile } from './stat-tile';
import { rosterAttentionHref } from '../lib/roster-links';

/** DistrictAdmin home teaser: the same counts as the compliance board with no
 * filters, each linking straight to the roster, plus a link to the full board. */
export function ComplianceSummaryBlock({ summary }: { summary: ComplianceSummaryDto }) {
  const denominator = `of ${summary.activeStudents} active students`;
  return (
    <HomeSection
      title="Compliance summary"
      data-testid="home-compliance-summary"
      action={
        <Link to="/educator/admin/compliance">
          <Button variant="secondary" size="sm" data-testid="home-compliance-summary-board-link">
            View compliance board
          </Button>
        </Link>
      }
    >
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {COMPLIANCE_SUMMARY_TILES.map((t) => (
          <StatTile
            key={t.key}
            label={t.label}
            value={summary[t.key]}
            denominator={denominator}
            href={rosterAttentionHref(t.attention)}
            tone={t.tone}
            data-testid={`home-compliance-summary-${t.key}`}
          />
        ))}
      </div>
    </HomeSection>
  );
}
