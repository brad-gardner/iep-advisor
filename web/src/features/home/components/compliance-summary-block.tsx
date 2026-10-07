import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { complianceTileLabel } from '@/lib/compliance-tile-label';
import { COMPLIANCE_SUMMARY_TILES } from '@/features/district-admin/lib/compliance-tiles';
import type { ComplianceSummaryDto } from '@/features/district-admin/types';
import { HomeSection } from './home-section';
import { StatTile } from './stat-tile';
import { rosterAttentionHref } from '../lib/roster-links';

/** DistrictAdmin home teaser: the same counts as the compliance board with no
 * filters, each linking straight to the roster, plus a link to the full board.
 *
 * `district-admin` is a staff-only namespace (multilingual plan phase 6) —
 * this component renders only behind the lazy staff route chunk (inside
 * `StaffHomePage` → `AdminHome`), never eagerly, so using it here is safe;
 * see `docs/i18n/README.md`'s "Staff and admin namespaces". It's listed here
 * only so `complianceTileLabel`'s Spanish bundle actually loads — the
 * `t()` calls below still come from `home`. */
export function ComplianceSummaryBlock({ summary }: { summary: ComplianceSummaryDto }) {
  const { t } = useTranslation(['home', 'district-admin']);
  const denominator = t('complianceSummary.denominator', { count: summary.activeStudents });
  return (
    <HomeSection
      title={t('complianceSummary.heading')}
      data-testid="home-compliance-summary"
      action={
        <Link to="/educator/admin/compliance">
          <Button variant="secondary" size="sm" data-testid="home-compliance-summary-board-link">
            {t('complianceSummary.viewBoard')}
          </Button>
        </Link>
      }
    >
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {COMPLIANCE_SUMMARY_TILES.map((tile) => (
          <StatTile
            key={tile.key}
            label={complianceTileLabel(tile.key)}
            value={summary[tile.key]}
            denominator={denominator}
            href={rosterAttentionHref(tile.attention)}
            tone={tile.tone}
            data-testid={`home-compliance-summary-${tile.key}`}
          />
        ))}
      </div>
    </HomeSection>
  );
}
