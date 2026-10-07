import { useTranslation } from 'react-i18next';
import type { AttentionFilter } from '@/features/educator/types';
import { formatDate } from '@/lib/format-date';
import { HomeSection } from './home-section';
import { StatTile } from './stat-tile';
import { rosterAttentionHref } from '../lib/roster-links';
import type { RosterAttentionDto } from '../types';

type RosterAttentionLabelKey =
  | 'rosterAttention.overdueAnnual'
  | 'rosterAttention.overdueReeval'
  | 'rosterAttention.due30'
  | 'rosterAttention.unknownDates'
  | 'rosterAttention.noLead'
  | 'rosterAttention.noFamily';

const TILES: {
  key: keyof RosterAttentionDto;
  labelKey: RosterAttentionLabelKey;
  attention: AttentionFilter;
  tone?: 'warning' | 'danger';
}[] = [
  { key: 'overdueAnnual', labelKey: 'rosterAttention.overdueAnnual', attention: 'OverdueAnnual', tone: 'danger' },
  { key: 'overdueReeval', labelKey: 'rosterAttention.overdueReeval', attention: 'OverdueReeval', tone: 'danger' },
  { key: 'due30', labelKey: 'rosterAttention.due30', attention: 'Due30', tone: 'warning' },
  { key: 'unknownDates', labelKey: 'rosterAttention.unknownDates', attention: 'UnknownDates', tone: 'warning' },
  { key: 'noLead', labelKey: 'rosterAttention.noLead', attention: 'NoCaseManager' },
  { key: 'noFamily', labelKey: 'rosterAttention.noFamily', attention: 'NoLinkedParent' },
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
  const { t } = useTranslation('home');
  const denominator = t('rosterAttention.asOf', { date: formatDate(generatedAt) });
  return (
    <HomeSection title={t('rosterAttention.title')} data-testid="home-roster-attention">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {TILES.map((tile) => (
          <StatTile
            key={tile.key}
            label={t(tile.labelKey)}
            value={counts[tile.key]}
            denominator={denominator}
            href={rosterAttentionHref(tile.attention)}
            tone={tile.tone}
            data-testid={`home-roster-attention-${tile.key}`}
          />
        ))}
      </div>
    </HomeSection>
  );
}
