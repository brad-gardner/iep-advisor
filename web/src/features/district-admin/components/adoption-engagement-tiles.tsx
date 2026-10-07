import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { BarChart } from '@/components/ui/charts/bar-chart';
import { loadErrorText } from '@/lib/api-error';
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
  const { t } = useTranslation('district-admin');
  const { adoption, engagement, adoptionError, engagementError, isLoading, error, retry } =
    useAdoptionEngagement(schoolId);

  if (isLoading) {
    return (
      <Card data-testid="adoption-engagement-loading">
        <div className="flex justify-center py-6">
          <Spinner label={t('adoptionEngagement.loading')} />
        </div>
      </Card>
    );
  }

  // Both endpoints failed — nothing to render at all. This joint-failure
  // case can't tell which endpoint produced a generic (non-server) failure,
  // so it uses a neutral combined message rather than attributing it to
  // either one.
  if (error) {
    return (
      <Card data-testid="adoption-engagement-error">
        <div role="alert">
          <Notice variant="error" title={loadErrorText(error, t('adoptionEngagement.errors.combined')) ?? ''}>
            <Button
              variant="secondary"
              className="mt-2"
              onClick={retry}
              data-testid="adoption-engagement-retry"
            >
              {t('adoptionEngagement.tryAgain')}
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
          <Notice
            variant="error"
            title={loadErrorText(adoptionError, t('adoptionEngagement.errors.adoption')) ?? ''}
            data-testid="adoption-engagement-adoption-error"
          >
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="adoption-engagement-retry">
              {t('adoptionEngagement.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}
      {engagementError && (
        <div role="alert">
          <Notice
            variant="error"
            title={loadErrorText(engagementError, t('adoptionEngagement.errors.engagement')) ?? ''}
            data-testid="adoption-engagement-engagement-error"
          >
            <Button variant="secondary" className="mt-2" onClick={retry} data-testid="adoption-engagement-retry">
              {t('adoptionEngagement.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {adoption && (
          <>
            <StatTile
              label={t('adoptionEngagement.staffActive')}
              value={adoption.staffActiveLast14}
              denominator={t('adoptionEngagement.ofStaffTotal', { count: adoption.staffTotal, rule: adoption.activeRule })}
              data-testid="adoption-staff-active"
            />
            <StatTile
              label={t('adoptionEngagement.draftsStarted')}
              value={adoption.draftsStarted}
              denominator={t('adoptionEngagement.lastDays', { count: adoption.days })}
              data-testid="adoption-drafts-started"
            />
            <StatTile
              label={t('adoptionEngagement.draftsFinalized')}
              value={adoption.draftsFinalized}
              denominator={t('adoptionEngagement.lastDays', { count: adoption.days })}
              data-testid="adoption-drafts-finalized"
            />
          </>
        )}
        {engagement && (
          <StatTile
            label={t('adoptionEngagement.familiesLinked')}
            value={engagement.studentsWithFamilyLink}
            denominator={t('adoptionEngagement.ofActiveStudents', { count: engagement.activeStudents })}
            data-testid="engagement-family-linked"
          />
        )}
      </div>

      {adoption && adoption.bySchool.length > 1 && (
        <BarChart
          title={t('adoptionEngagement.chartTitle')}
          data-testid="adoption-by-school-chart"
          data={adoption.bySchool.map((s) => ({ label: s.schoolName, value: s.staffActive }))}
          max={Math.max(...adoption.bySchool.map((s) => s.staffTotal), 1)}
        />
      )}
    </div>
  );
}
