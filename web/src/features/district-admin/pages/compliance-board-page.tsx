import { useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Skeleton } from '@/components/ui/skeleton';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import { ORG_ROLE } from '@/features/educator/types';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { getComplianceBoard, getDistrictSchools } from '../api/district-api';
import { AdoptionEngagementTiles } from '../components/adoption-engagement-tiles';
import { ComplianceFilters } from '../components/compliance-filters';
import { ComplianceSchoolTable } from '../components/compliance-school-table';
import { ComplianceSummaryTiles } from '../components/compliance-summary-tiles';
import { complianceDateRange, type ComplianceRangeDays } from '../lib/date-range';
import type { ComplianceBoardDto, DistrictSchool } from '../types';

/**
 * District/school compliance board (`/educator/admin/compliance`): overdue and
 * upcoming procedural deadlines, filterable by school (DistrictAdmin) and due
 * date range. Every tile and per-school cell drills to the roster with the
 * matching filter, so the counts here and there always agree.
 */
export function ComplianceBoardPage() {
  usePageTitle('Compliance');
  const { profile } = useEducatorProfile();
  const isDistrictAdmin = profile?.orgRoleId === ORG_ROLE.DistrictAdmin;

  const [schoolId, setSchoolId] = useState<number | null>(null);
  const [rangeDays, setRangeDays] = useState<ComplianceRangeDays>(60);
  const [schools, setSchools] = useState<DistrictSchool[]>([]);
  const { from, to } = useMemo(() => complianceDateRange(rangeDays), [rangeDays]);

  const [retryToken, setRetryToken] = useState(0);
  const boardScope = isDistrictAdmin && schoolId != null ? String(schoolId) : 'own';
  const requestKey = `${boardScope}#${from}#${to}#${retryToken}`;
  const [loaded, setLoaded] = useState<{
    key: string;
    board: ComplianceBoardDto | null;
    error: string | null;
  } | null>(null);

  // DistrictAdmin needs the school list for the picker; SchoolAdmin never
  // sees one (their `schoolId` is ignored/forced server-side).
  useEffect(() => {
    if (!isDistrictAdmin) return;
    let active = true;
    (async () => {
      try {
        const response = await getDistrictSchools();
        if (active && response.success && response.data) setSchools(response.data);
      } catch {
        if (active) setSchools([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [isDistrictAdmin]);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await getComplianceBoard({
          schoolId: isDistrictAdmin && schoolId != null ? schoolId : undefined,
          from,
          to,
        });
        if (!active) return;
        if (response.success && response.data) {
          setLoaded({ key: requestKey, board: response.data, error: null });
        } else {
          setLoaded({
            key: requestKey,
            board: null,
            error: response.message ?? 'Could not load the compliance board',
          });
        }
      } catch (err) {
        if (active) {
          setLoaded({
            key: requestKey,
            board: null,
            error: apiErrorMessage(err, 'Could not load the compliance board'),
          });
        }
      }
    })();
    return () => {
      active = false;
    };
  }, [schoolId, from, to, isDistrictAdmin, requestKey]);

  const isLoading = loaded?.key !== requestKey;
  const board = isLoading ? null : (loaded?.board ?? null);
  const error = isLoading ? null : (loaded?.error ?? null);

  return (
    <PageLayout
      title="Compliance"
      subtitle="Overdue and upcoming procedural deadlines, with the evidence behind them."
      data-testid="compliance-board-page"
    >
      <ComplianceFilters
        schools={isDistrictAdmin ? schools : undefined}
        schoolId={schoolId}
        onSchoolChange={setSchoolId}
        rangeDays={rangeDays}
        onRangeChange={setRangeDays}
      />

      {error && (
        <Notice variant="error" title={error}>
          <Button
            variant="secondary"
            className="mt-2"
            onClick={() => setRetryToken((t) => t + 1)}
            data-testid="compliance-board-retry"
          >
            Try again
          </Button>
        </Notice>
      )}

      {isLoading && !error && (
        <div className="space-y-4" data-testid="compliance-board-loading">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      )}

      {!isLoading && !error && board && (
        <>
          <ComplianceSummaryTiles summary={board.summary} drill={board.drill} schoolId={schoolId} />
          <ComplianceSchoolTable rows={board.bySchool} drill={board.drill} />
          <AdoptionEngagementTiles schoolId={isDistrictAdmin ? schoolId : null} />
        </>
      )}
    </PageLayout>
  );
}
