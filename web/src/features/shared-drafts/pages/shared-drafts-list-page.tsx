import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FileText } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Spinner } from '@/components/ui/spinner';
import { usePageTitle } from '@/hooks/use-page-title';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { getSharedDrafts } from '../api/shared-drafts-api';
import { SHARED_DRAFT_STATUS_BADGE } from '../lib/status-badge';
import type { SharedDraftRevisionDto } from '../types';

type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/** Every revision ever shared for this child, newest first — the parent's
 *  entry point onto the reading view (`/children/:childId/shared-drafts/:rev`). */
export function SharedDraftsListPage() {
  const { t } = useTranslation(['shared-drafts', 'common']);
  const { childId: childIdParam } = useParams<{ childId: string }>();
  const childId = Number(childIdParam);
  usePageTitle(t('listPage.pageTitle'));

  const [drafts, setDrafts] = useState<SharedDraftRevisionDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    if (!childId) return;
    let active = true;
    (async () => {
      setDrafts(null);
      setError(null);
      try {
        const res = await getSharedDrafts(childId);
        if (!active) return;
        if (res.success && res.data) setDrafts(res.data);
        else setError(res.message ? { kind: 'server', message: res.message } : { kind: 'generic' });
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — the generic fallback translates at render
    // time below, from `error`'s stored KIND (same idiom as `useHome`).
  }, [childId, retryToken]);

  const backTo = `/children/${childId}/overview`;

  if (drafts === null && error === null) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label={t('listPage.loading')} />
      </div>
    );
  }

  if (error) {
    return (
      <PageLayout title={t('listPage.pageTitle')} breadcrumb={[{ label: t('listPage.breadcrumbOverview'), to: backTo }]}>
        <div role="alert">
          <Notice variant="error" title={error.kind === 'server' ? error.message : t('listPage.loadError')}>
            <Button variant="secondary" className="mt-2" onClick={() => setRetryToken((n) => n + 1)}>
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      </PageLayout>
    );
  }

  const revisions = drafts ?? [];

  return (
    <PageLayout title={t('listPage.pageTitle')} breadcrumb={[{ label: t('listPage.breadcrumbOverview'), to: backTo }]}>
      {revisions.length === 0 ? (
        <EmptyState icon={FileText} title={t('listPage.emptyTitle')} description={t('listPage.emptyDescription')} />
      ) : (
        <ul className="space-y-3" data-testid="shared-drafts-list">
          {revisions.map((rev) => (
            <li key={rev.id}>
              <Link to={`/children/${childId}/shared-drafts/${rev.id}`} data-testid={`shared-drafts-list-item-${rev.id}`}>
                <Card className="flex items-center justify-between gap-4 transition-colors hover:border-brand-teal-300">
                  <span className="flex flex-col">
                    <span className="text-sm font-medium text-brand-slate-800">
                      {t('listPage.revisionLine', { documentType: rev.documentTypeDisplayName, number: rev.revisionNumber })}
                    </span>
                    <span className="text-xs text-brand-slate-500">
                      {t('listPage.sharedByLine', { date: formatDate(rev.sharedAt), name: rev.sharedByName })}
                    </span>
                  </span>
                  <Badge variant={SHARED_DRAFT_STATUS_BADGE[rev.status]}>{t(`status.${rev.status}`)}</Badge>
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </PageLayout>
  );
}
