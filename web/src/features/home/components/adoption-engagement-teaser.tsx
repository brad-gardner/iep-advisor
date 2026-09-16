import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
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
  const { adoption, engagement, adoptionError, engagementError, isLoading, error, retry } =
    useAdoptionEngagement(null);

  return (
    <HomeSection
      title="Adoption & family engagement"
      data-testid="home-adoption-engagement"
      action={
        <Link to={BOARD_LINK}>
          <Button variant="secondary" size="sm" data-testid="home-adoption-engagement-board-link">
            View compliance board
          </Button>
        </Link>
      }
    >
      {isLoading && (
        <div className="flex justify-center py-4">
          <Spinner label="Loading adoption data…" />
        </div>
      )}

      {!isLoading && error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button
              size="sm"
              variant="secondary"
              onClick={retry}
              data-testid="home-adoption-engagement-retry"
            >
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {!isLoading && !error && (adoptionError || engagementError) && (
        <div role="alert" className="mb-3">
          <Notice variant="error" title={adoptionError ?? engagementError ?? ''}>
            <Button
              size="sm"
              variant="secondary"
              onClick={retry}
              data-testid="home-adoption-engagement-retry"
            >
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {!isLoading && !error && (adoption || engagement) && (
        <div className="grid gap-3 sm:grid-cols-2">
          {adoption && (
            <>
              <StatTile
                label="Staff active (14 days)"
                value={adoption.staffActiveLast14}
                denominator={`of ${adoption.staffTotal} staff`}
                href={BOARD_LINK}
                data-testid="home-adoption-staff-active"
              />
              <StatTile
                label="Drafts started"
                value={adoption.draftsStarted}
                denominator={`Last ${adoption.days} days`}
                href={BOARD_LINK}
                data-testid="home-adoption-drafts-started"
              />
            </>
          )}
          {engagement && (
            <>
              <StatTile
                label="Families linked"
                value={engagement.studentsWithFamilyLink}
                denominator={`of ${engagement.activeStudents} active students`}
                href={BOARD_LINK}
                data-testid="home-engagement-family-linked"
              />
              <StatTile
                label="Drafts shared with family"
                value={engagement.draftsShared}
                denominator={`of ${engagement.activeStudents} active students`}
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
