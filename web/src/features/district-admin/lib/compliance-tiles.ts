import type { AttentionFilter } from '@/features/educator/types';
import type { ComplianceSummaryDto } from '../types';

export interface ComplianceTileDef {
  key: keyof Omit<ComplianceSummaryDto, 'activeStudents' | 'dueInRange'>;
  label: string;
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
 */
export const COMPLIANCE_SUMMARY_TILES: ComplianceTileDef[] = [
  { key: 'overdueAnnual', label: 'Overdue annual reviews', attention: 'OverdueAnnual', tone: 'danger' },
  { key: 'overdueReeval', label: 'Overdue reevaluations', attention: 'OverdueReeval', tone: 'danger' },
  { key: 'due30', label: 'Due within 30 days', attention: 'Due30', tone: 'warning' },
  { key: 'due60', label: 'Due within 60 days', attention: 'Due60', tone: 'warning' },
  { key: 'unknownDates', label: 'Unknown dates', attention: 'UnknownDates', tone: 'warning' },
  { key: 'noLead', label: 'No case manager', attention: 'NoCaseManager' },
];
