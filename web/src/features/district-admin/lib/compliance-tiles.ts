import type { AttentionFilter } from '@/features/educator/types';
import type { ComplianceSummaryDto } from '../types';

export interface ComplianceTileDef {
  key: keyof Omit<ComplianceSummaryDto, 'activeStudents' | 'dueInRange'>;
  attention: AttentionFilter;
  tone?: 'warning' | 'danger';
}

/**
 * The compliance board's six always-anchored-on-today buckets — shared by the
 * DistrictAdmin home teaser (`compliance-summary-block.tsx`) and the full
 * board (`compliance-summary-tiles.tsx`/`compliance-school-table.tsx`), each
 * of which still builds its own href (roster deep link vs. the board's
 * server-provided `drill` map). `dueInRange` is deliberately excluded: it only
 * makes sense with the board's own `from`/`to` picker, which the home teaser
 * doesn't have.
 *
 * No `label` field here (removed in the multilingual plan's phase 6): each
 * caller renders `complianceTileLabel(tile.key)`
 * (`@/lib/compliance-tile-label.ts`) instead, which resolves the translated
 * text at render time rather than baking English in at module load.
 */
export const COMPLIANCE_SUMMARY_TILES: ComplianceTileDef[] = [
  { key: 'overdueAnnual', attention: 'OverdueAnnual', tone: 'danger' },
  { key: 'overdueReeval', attention: 'OverdueReeval', tone: 'danger' },
  { key: 'due30', attention: 'Due30', tone: 'warning' },
  { key: 'due60', attention: 'Due60', tone: 'warning' },
  { key: 'unknownDates', attention: 'UnknownDates', tone: 'warning' },
  { key: 'noLead', attention: 'NoCaseManager' },
];
