import { DistrictOverviewCard } from '@/features/district-admin/components/district-overview-card';
import { DistrictDashboardTiles } from '@/features/district-admin/components/district-dashboard-tiles';
import { SetupChecklistCard } from '@/features/district-admin/components/setup-checklist-card';
import { formatDate } from '@/lib/format-date';
import { AdoptionEngagementTeaser } from './adoption-engagement-teaser';
import { ComplianceSummaryBlock } from './compliance-summary-block';
import { OverdueByCaseManagerTable } from './overdue-by-case-manager-table';
import { RosterAttentionTiles } from './roster-attention-tiles';
import { ThisWeekSection } from './this-week-section';
import { UnsignedFinalizedSection } from './unsigned-finalized-section';
import type { StaffHomeDto } from '../types';

interface AdminHomeProps {
  staff: StaffHomeDto;
  isDistrict: boolean;
  /** `HomeDto.generatedAt` — threaded down to `RosterAttentionTiles` for its
   * "As of …" date-range context. */
  generatedAt: string;
}

/**
 * SchoolAdmin / DistrictAdmin home body. Both tiers get the same operational
 * sections (this week's meetings in scope, the overdue/at-risk table sorted by
 * student, unsigned finalized documents, roster attention) plus the existing
 * district oversight tiles; DistrictAdmin additionally gets the compliance
 * summary and adoption/engagement teasers up top.
 */
export function AdminHome({ staff, isDistrict, generatedAt }: AdminHomeProps) {
  return (
    <div className="space-y-6" data-testid="staff-home-admin">
      {isDistrict && <SetupChecklistCard />}
      {isDistrict && <DistrictOverviewCard />}
      {isDistrict && staff.complianceSummary && (
        <ComplianceSummaryBlock summary={staff.complianceSummary} />
      )}
      {isDistrict && <AdoptionEngagementTeaser />}

      <ThisWeekSection
        title={isDistrict ? 'Meetings this week' : 'Meetings this week in my building'}
        subtitle={`${formatDate(staff.weekStart)} – ${formatDate(staff.weekEnd)}`}
        meetings={staff.meetingsThisWeek}
        showBriefNote
      />

      {staff.overdueByCaseManager && (
        <OverdueByCaseManagerTable rows={staff.overdueByCaseManager} />
      )}

      {staff.unsignedFinalized && <UnsignedFinalizedSection items={staff.unsignedFinalized} />}

      {staff.rosterAttention && (
        <RosterAttentionTiles counts={staff.rosterAttention} generatedAt={generatedAt} />
      )}

      <DistrictDashboardTiles />
    </div>
  );
}
