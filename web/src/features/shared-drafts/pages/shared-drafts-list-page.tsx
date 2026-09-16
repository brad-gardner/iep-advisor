import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
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

/** Every revision ever shared for this child, newest first — the parent's
 *  entry point onto the reading view (`/children/:childId/shared-drafts/:rev`). */
export function SharedDraftsListPage() {
  const { childId: childIdParam } = useParams<{ childId: string }>();
  const childId = Number(childIdParam);
  usePageTitle('Shared drafts');

  const [drafts, setDrafts] = useState<SharedDraftRevisionDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
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
        else setError(res.message ?? 'Could not load shared drafts.');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load shared drafts.'));
      }
    })();
    return () => {
      active = false;
    };
  }, [childId, retryToken]);

  const backTo = `/children/${childId}/overview`;

  if (drafts === null && error === null) {
    return (
      <div className="flex justify-center py-12">
        <Spinner label="Loading shared drafts…" />
      </div>
    );
  }

  if (error) {
    return (
      <PageLayout title="Shared drafts" breadcrumb={[{ label: 'Overview', to: backTo }]}>
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button variant="secondary" className="mt-2" onClick={() => setRetryToken((t) => t + 1)}>
              Try again
            </Button>
          </Notice>
        </div>
      </PageLayout>
    );
  }

  const revisions = drafts ?? [];

  return (
    <PageLayout title="Shared drafts" breadcrumb={[{ label: 'Overview', to: backTo }]}>
      {revisions.length === 0 ? (
        <EmptyState
          icon={FileText}
          title="No shared drafts yet"
          description="When your child's school shares a draft IEP or ETR, it will show up here."
        />
      ) : (
        <ul className="space-y-3" data-testid="shared-drafts-list">
          {revisions.map((rev) => (
            <li key={rev.id}>
              <Link to={`/children/${childId}/shared-drafts/${rev.id}`} data-testid={`shared-drafts-list-item-${rev.id}`}>
                <Card className="flex items-center justify-between gap-4 transition-colors hover:border-brand-teal-300">
                  <span className="flex flex-col">
                    <span className="text-sm font-medium text-brand-slate-800">
                      {rev.documentTypeDisplayName} · Revision {rev.revisionNumber}
                    </span>
                    <span className="text-xs text-brand-slate-500">
                      Shared {formatDate(rev.sharedAt)} by {rev.sharedByName}
                    </span>
                  </span>
                  <Badge variant={SHARED_DRAFT_STATUS_BADGE[rev.status]}>{rev.status}</Badge>
                </Card>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </PageLayout>
  );
}
