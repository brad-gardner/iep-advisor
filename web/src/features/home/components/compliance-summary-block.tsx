import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import type { AttentionFilter } from '@/features/educator/types';
import type { ComplianceSummaryDto } from '@/features/district-admin/types';
import { HomeSection } from './home-section';
import { StatTile } from './stat-tile';
import { rosterAttentionHref } from '../lib/roster-links';

const TILES: {
  key: keyof Omit<ComplianceSummaryDto, 'activeStudents'>;
  label: string;
  attention: AttentionFilter;
  tone?: 'warning' | 'danger';
}[] = [
  { key: 'overdueAnnual', label: 'Overdue annual reviews', attention: 'OverdueAnnual', tone: 'danger' },
  { key: 'overdueReeval', label: 'Overdue reevaluations', attention: 'OverdueReeval', tone: 'danger' },
  { key: 'due30', label: 'Due within 30 days', attention: 'Due30', tone: 'warning' },
  { key: 'due60', label: 'Due within 60 days', attention: 'Due60', tone: 'warning' },
  { key: 'unknownDates', label: 'Unknown dates', attention: 'UnknownDates', tone: 'warning' },
  { key: 'noLead', label: 'No case manager', attention: 'NoCaseManager' },
];

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
        {TILES.map((t) => (
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
