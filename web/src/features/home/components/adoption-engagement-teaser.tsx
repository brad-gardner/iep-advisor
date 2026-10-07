import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { loadErrorText } from '@/lib/api-error';
import { useAdoptionEngagement } from '@/features/district-admin/hooks/use-adoption-engagement';
import { HomeSection } from './home-section';
import { StatTile } from './stat-tile';

const BOARD_LINK = '/educator/admin/compliance';

/**
 * DistrictAdmin home teaser: district-wide adoption + family-engagement
 * numbers, framed as evidence rather than a to-do list. Shares its fetch
 * (`useAdoptionEngagement`) with the full compliance board, so a failure here
 * never blocks the rest of the home.
 */
export function AdoptionEngagementTeaser() {
  const { t } = useTranslation(['home', 'common']);
  const { adoption, engagement, adoptionError, engagementError, isLoading, error, retry } =
    useAdoptionEngagement(null);

  return (
    <HomeSection
      title={t('adoptionEngagement.heading')}
      data-testid="home-adoption-engagement"
      action={
        <Link to={BOARD_LINK}>
          <Button variant="secondary" size="sm" data-testid="home-adoption-engagement-board-link">
            {t('adoptionEngagement.viewBoard')}
          </Button>
        </Link>
      }
    >
      {isLoading && (
        <div className="flex justify-center py-4">
          <Spinner label={t('adoptionEngagement.loading')} />
        </div>
      )}

      {!isLoading && error && (
        <div role="alert">
          <Notice variant="error" title={loadErrorText(error, t('common:ui.genericError')) ?? ''}>
            <Button
              size="sm"
              variant="secondary"
              onClick={retry}
              data-testid="home-adoption-engagement-retry"
            >
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {!isLoading && !error && (adoptionError || engagementError) && (
        <div role="alert" className="mb-3">
          <Notice
            variant="error"
            title={
              loadErrorText(adoptionError, t('common:ui.genericError')) ??
              loadErrorText(engagementError, t('common:ui.genericError')) ??
              ''
            }
          >
            <Button
              size="sm"
              variant="secondary"
              onClick={retry}
              data-testid="home-adoption-engagement-retry"
            >
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {!isLoading && !error && (adoption || engagement) && (
        <div className="grid gap-3 sm:grid-cols-2">
          {adoption && (
            <>
              <StatTile
                label={t('adoptionEngagement.staffActive')}
                value={adoption.staffActiveLast14}
                denominator={t('adoptionEngagement.staffDenominator', { count: adoption.staffTotal })}
                href={BOARD_LINK}
                data-testid="home-adoption-staff-active"
              />
              <StatTile
                label={t('adoptionEngagement.draftsStarted')}
                value={adoption.draftsStarted}
                denominator={t('adoptionEngagement.draftsStartedDenominator', { days: adoption.days })}
                href={BOARD_LINK}
                data-testid="home-adoption-drafts-started"
              />
            </>
          )}
          {engagement && (
            <>
              <StatTile
                label={t('adoptionEngagement.familiesLinked')}
                value={engagement.studentsWithFamilyLink}
                denominator={t('adoptionEngagement.activeStudentsDenominator', { count: engagement.activeStudents })}
                href={BOARD_LINK}
                data-testid="home-engagement-family-linked"
              />
              <StatTile
                label={t('adoptionEngagement.draftsShared')}
                value={engagement.draftsShared}
                denominator={t('adoptionEngagement.activeStudentsDenominator', { count: engagement.activeStudents })}
                href={BOARD_LINK}
                data-testid="home-engagement-drafts-shared"
              />
            </>
          )}
        </div>
      )}
    </HomeSection>
  );
}
