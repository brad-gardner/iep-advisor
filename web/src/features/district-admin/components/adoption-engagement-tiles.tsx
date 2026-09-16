import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { BarChart } from '@/components/ui/charts/bar-chart';
import { StatTile } from '@/features/home/components/stat-tile';
import { useAdoptionEngagement } from '../hooks/use-adoption-engagement';

interface AdoptionEngagementTilesProps {
  /** `null` = district-wide (DistrictAdmin with no school filter, or SchoolAdmin
   * whose `schoolId` param is ignored server-side anyway). */
  schoolId: number | null;
}

/**
 * Adoption ("is staff actually using it") and family-engagement ("evidence for
 * the sale conversation") tiles on the compliance board, plus a per-school
 * staff-activity bar chart.
 */
export function AdoptionEngagementTiles({ schoolId }: AdoptionEngagementTilesProps) {
  const { adoption, engagement, adoptionError, engagementError, isLoading, error, retry } =
    useAdoptionEngagement(schoolId);

  if (isLoading) {
    return (
      <Card data-testid="adoption-engagement-loading">
        <div className="flex justify-center py-6">
          <Spinner label="Loading adoption data…" />
        </div>
      </Card>
    );
  }

  // Both endpoints failed — nothing to render at all.
  if (error) {
    return (
      <Card data-testid="adoption-engagement-error">
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button
              variant="secondary"
              className="mt-2"
              onClick={retry}
              data-testid="adoption-engagement-retry"
            >
              Try again
            </Button>
          </Notice>
        </div>
      </Card>
    );
  }

  return (
    <div className="space-y-4" data-testid="adoption-engagement-tiles">
      {adoptionError && (
        <div role="alert">
          <Notice variant="error" title={adoptionError} data-testid="adoption-engagement-adoption-error">
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="adoption-engagement-retry">
              Try again
            </Button>
          </Notice>
        </div>
      )}
      {engagementError && (
        <div role="alert">
          <Notice
            variant="error"
            title={engagementError}
            data-testid="adoption-engagement-engagement-error"
          >
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="adoption-engagement-retry">
              Try again
            </Button>
          </Notice>
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {adoption && (
          <>
            <StatTile
              label="Staff active (14 days)"
              value={adoption.staffActiveLast14}
              denominator={`of ${adoption.staffTotal} staff · ${adoption.activeRule}`}
              data-testid="adoption-staff-active"
            />
            <StatTile
              label="Drafts started"
              value={adoption.draftsStarted}
              denominator={`Last ${adoption.days} days`}
              data-testid="adoption-drafts-started"
            />
            <StatTile
              label="Drafts finalized"
              value={adoption.draftsFinalized}
              denominator={`Last ${adoption.days} days`}
              data-testid="adoption-drafts-finalized"
            />
          </>
        )}
        {engagement && (
          <StatTile
            label="Families linked"
            value={engagement.studentsWithFamilyLink}
            denominator={`of ${engagement.activeStudents} active students`}
            data-testid="engagement-family-linked"
          />
        )}
      </div>

      {adoption && adoption.bySchool.length > 1 && (
        <BarChart
          title="Active staff by school"
          data-testid="adoption-by-school-chart"
          data={adoption.bySchool.map((s) => ({ label: s.schoolName, value: s.staffActive }))}
          max={Math.max(...adoption.bySchool.map((s) => s.staffTotal), 1)}
        />
      )}
    </div>
  );
}
