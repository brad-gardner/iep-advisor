import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { FileText } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { getMeetingSummary } from '../api/shared-drafts-api';
import type { MeetingSummaryDto } from '../types';

/** Parent read-only view of a sent post-meeting family summary. The server
 *  only returns a summary to a family caller once it's Sent (a draft-in-
 *  progress 404s), so "no summary yet" covers both "none exists" and "not
 *  sent yet" without the page needing to know which. */
export function MeetingSummaryPage() {
  const { childId: childIdParam, meetingId: meetingIdParam } = useParams<{
    childId: string;
    meetingId: string;
  }>();
  const childId = Number(childIdParam);
  const meetingId = Number(meetingIdParam);
  usePageTitle('Meeting summary');

  // `undefined` = still loading; `null` = loaded, none available.
  const [summary, setSummary] = useState<MeetingSummaryDto | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!meetingId) return;
    let active = true;
    getMeetingSummary(meetingId)
      .then((res) => {
        if (active) setSummary(res);
      })
      .catch((err: unknown) => {
        if (!active) return;
        setError(apiErrorMessage(err, 'Could not load this meeting summary.'));
        setSummary(null);
      });
    return () => {
      active = false;
    };
  }, [meetingId]);

  const backTo = `/children/${childId}/overview`;

  if (summary === undefined) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading meeting summary…" />
      </div>
    );
  }

  if (error) {
    return (
      <PageLayout title="Meeting summary unavailable" breadcrumb={[{ label: 'Overview', to: backTo }]}>
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      </PageLayout>
    );
  }

  if (!summary) {
    return (
      <PageLayout title="Meeting summary" breadcrumb={[{ label: 'Overview', to: backTo }]}>
        <EmptyState
          icon={FileText}
          title="No summary yet"
          description="Your child's school hasn't sent a summary for this meeting yet."
        />
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title="Meeting summary"
      subtitle={
        summary.sentAt
          ? `Sent ${formatDate(summary.sentAt)}${summary.sentByName ? ` by ${summary.sentByName}` : ''}`
          : undefined
      }
      breadcrumb={[{ label: 'Overview', to: backTo }]}
      data-testid="meeting-summary-page"
    >
      <Card>
        <p className="whitespace-pre-wrap text-sm text-brand-slate-800" data-testid="meeting-summary-body">
          {summary.body}
        </p>
      </Card>
    </PageLayout>
  );
}
