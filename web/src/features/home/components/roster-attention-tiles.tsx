import type { AttentionFilter } from '@/features/educator/types';
import { formatDate } from '@/lib/format-date';
import { HomeSection } from './home-section';
import { StatTile } from './stat-tile';
import { rosterAttentionHref } from '../lib/roster-links';
import type { RosterAttentionDto } from '../types';

const TILES: {
  key: keyof RosterAttentionDto;
  label: string;
  attention: AttentionFilter;
  tone?: 'warning' | 'danger';
}[] = [
  { key: 'overdueAnnual', label: 'Overdue annual reviews', attention: 'OverdueAnnual', tone: 'danger' },
  { key: 'overdueReeval', label: 'Overdue reevaluations', attention: 'OverdueReeval', tone: 'danger' },
  { key: 'due30', label: 'Due within 30 days', attention: 'Due30', tone: 'warning' },
  { key: 'unknownDates', label: 'Unknown dates', attention: 'UnknownDates', tone: 'warning' },
  { key: 'noLead', label: 'No case manager', attention: 'NoCaseManager' },
  { key: 'noFamily', label: 'No linked family', attention: 'NoLinkedParent' },
];

interface RosterAttentionTilesProps {
  counts: RosterAttentionDto;
  /** `HomeDto.generatedAt` — every tile's date-range context ("As of …"), since
   * `RosterAttentionDto` carries no denominator count of its own. */
  generatedAt: string;
}

/** SchoolAdmin/DistrictAdmin "Roster attention" tiles — no lead case manager,
 * no linked family, unknown dates, and the procedural-deadline buckets. Every
 * tile drills to the roster with the matching filter. */
export function RosterAttentionTiles({ counts, generatedAt }: RosterAttentionTilesProps) {
  const denominator = `As of ${formatDate(generatedAt)}`;
  return (
    <HomeSection title="Roster attention" data-testid="home-roster-attention">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {TILES.map((t) => (
          <StatTile
            key={t.key}
            label={t.label}
            value={counts[t.key]}
            denominator={denominator}
            href={rosterAttentionHref(t.attention)}
            tone={t.tone}
            data-testid={`home-roster-attention-${t.key}`}
          />
        ))}
      </div>
    </HomeSection>
  );
}
