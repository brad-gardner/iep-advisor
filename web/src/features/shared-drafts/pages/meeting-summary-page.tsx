import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FileText } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Markdown } from '@/components/ui/markdown';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { GeneratedLanguageNotice } from '@/lib/i18n/generated-language-notice';
import { getMeetingSummary } from '../api/shared-drafts-api';
import type { MeetingSummaryDto } from '../types';

// A server-provided message is already resolved text; the generic fallback
// is translated at RENDER time (see the `error` render block below), not
// stored pre-translated here, so the mount effect never needs `t` in its
// dependency array (same idiom as `useHome`).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/** Parent read-only view of a sent post-meeting family summary. The server
 *  only returns a summary to a family caller once it's Sent (a draft-in-
 *  progress 404s), so "no summary yet" covers both "none exists" and "not
 *  sent yet" without the page needing to know which. */
export function MeetingSummaryPage() {
  const { t } = useTranslation('shared-drafts');
  const { childId: childIdParam, meetingId: meetingIdParam } = useParams<{
    childId: string;
    meetingId: string;
  }>();
  const childId = Number(childIdParam);
  const meetingId = Number(meetingIdParam);
  usePageTitle(t('meetingSummaryPage.pageTitle'));

  // `undefined` = still loading; `null` = loaded, none available.
  const [summary, setSummary] = useState<MeetingSummaryDto | null | undefined>(undefined);
  const [error, setError] = useState<LoadError | null>(null);

  useEffect(() => {
    if (!meetingId) return;
    let active = true;
    getMeetingSummary(meetingId)
      .then((res) => {
        if (active) setSummary(res);
      })
      .catch((err: unknown) => {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
        setSummary(null);
      });
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): the generic fallback is
    // translated below, at render, from `error`'s stored KIND, so it already
    // follows the active language with no refetch needed.
  }, [meetingId]);

  const backTo = `/children/${childId}/overview`;

  if (summary === undefined) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('meetingSummaryPage.loading')} />
      </div>
    );
  }

  if (error) {
    return (
      <PageLayout title={t('meetingSummaryPage.unavailableTitle')} breadcrumb={[{ label: t('meetingSummaryPage.breadcrumbOverview'), to: backTo }]}>
        <div role="alert">
          <Notice variant="error" title={error.kind === 'server' ? error.message : t('meetingSummaryPage.loadError')} />
        </div>
      </PageLayout>
    );
  }

  if (!summary) {
    return (
      <PageLayout title={t('meetingSummaryPage.pageTitle')} breadcrumb={[{ label: t('meetingSummaryPage.breadcrumbOverview'), to: backTo }]}>
        <EmptyState icon={FileText} title={t('meetingSummaryPage.emptyTitle')} description={t('meetingSummaryPage.emptyDescription')} />
      </PageLayout>
    );
  }

  return (
    <PageLayout
      title={t('meetingSummaryPage.pageTitle')}
      subtitle={
        summary.sentAt
          ? summary.sentByName
            ? t('meetingSummaryPage.sentLineByName', { date: formatDate(summary.sentAt), name: summary.sentByName })
            : t('meetingSummaryPage.sentLine', { date: formatDate(summary.sentAt) })
          : undefined
      }
      breadcrumb={[{ label: t('meetingSummaryPage.breadcrumbOverview'), to: backTo }]}
      data-testid="meeting-summary-page"
    >
      <Card>
        <GeneratedLanguageNotice generatedLanguage={summary.generatedLanguage} className="mb-3" />
        <Markdown content={summary.body} data-testid="meeting-summary-body" />
      </Card>
    </PageLayout>
  );
}
