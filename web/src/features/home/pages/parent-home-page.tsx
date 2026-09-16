import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Skeleton } from '@/components/ui/skeleton';
import { usePageTitle } from '@/hooks/use-page-title';
import { useAuth } from '@/features/auth/hooks/use-auth';
import { DashboardChildrenSection } from '@/features/children/components/dashboard-children-section';
import { DocumentsToReviewSection } from '../components/documents-to-review-section';
import { EmptyHint } from '../components/empty-hint';
import { LegacyParentHome } from '../components/legacy-parent-home';
import { NextMeetingCard } from '../components/next-meeting-card';
import { ParentSetupNotices } from '../components/parent-setup-notices';
import { RecentProgressReportsSection } from '../components/recent-progress-reports-section';
import { useHome } from '../hooks/use-home';
import type { HomeMeetingDto, ParentNextMeetingDto } from '../types';

/**
 * The parent (and Mode-C consumer-parent) home. A parent with at least one
 * school-linked child gets the new operational sections (next meeting,
 * documents to review, progress reports) with the legacy setup notices
 * demoted below; a parent with no school link at all sees the previous
 * dashboard body unchanged (see `LegacyParentHome`).
 */
export function ParentHomePage() {
  const { user } = useAuth();
  const { home, isLoading, error, retry } = useHome();
  // Reflects a successful RSVP into the rendered next-meeting card without a
  // full home refetch. Reset whenever the underlying meeting id changes.
  const [meetingOverride, setMeetingOverride] = useState<ParentNextMeetingDto | null>(null);

  const title = `Welcome, ${user?.firstName ?? ''}`.trim();
  usePageTitle(title || 'Home');

  if (isLoading) {
    return (
      <PageLayout title={title}>
        <div className="space-y-6" role="status" aria-label="Loading your home">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-40 w-full" />
          <span className="sr-only">Loading…</span>
        </div>
      </PageLayout>
    );
  }

  if (error || !home || home.kind !== 'Parent' || !home.parent) {
    return (
      <PageLayout title={title}>
        <Card data-testid="parent-home-error">
          <div role="alert">
            <Notice variant="error" title={error ?? "Couldn't load your home"}>
              <Button variant="secondary" className="mt-2" onClick={retry} data-testid="parent-home-retry">
                Try again
              </Button>
            </Notice>
          </div>
        </Card>
      </PageLayout>
    );
  }

  const { parent } = home;
  const hasSchoolLink = parent.children.some((c) => c.hasSchoolLink);

  if (!hasSchoolLink) {
    return (
      <PageLayout title={title}>
        <LegacyParentHome user={user} />
      </PageLayout>
    );
  }

  const nextMeeting: ParentNextMeetingDto | null =
    meetingOverride && meetingOverride.id === parent.nextMeeting?.id
      ? meetingOverride
      : parent.nextMeeting;

  const handleMeetingUpdated = (updated: HomeMeetingDto) => {
    if (!parent.nextMeeting) return;
    setMeetingOverride({ ...parent.nextMeeting, ...updated });
  };

  return (
    <PageLayout title={title}>
      <div className="space-y-6" data-testid="parent-home-operational">
        {nextMeeting ? (
          <NextMeetingCard
            meeting={nextMeeting}
            showCountdown
            subtitle={nextMeeting.childName}
            onUpdated={handleMeetingUpdated}
            data-testid="parent-home-next-meeting"
          />
        ) : (
          <Card data-testid="parent-home-next-meeting-empty">
            <EmptyHint>No upcoming meetings scheduled.</EmptyHint>
          </Card>
        )}

        <DocumentsToReviewSection items={parent.documentsToReview} />
        <DashboardChildrenSection />
        <RecentProgressReportsSection items={parent.recentProgressReports} />
        <ParentSetupNotices user={user} notices={parent.setupNotices} />
      </div>
    </PageLayout>
  );
}
