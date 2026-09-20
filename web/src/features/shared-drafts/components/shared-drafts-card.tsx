import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { formatDate } from '@/lib/format-date';
import { getSharedDrafts } from '../api/shared-drafts-api';
import { SHARED_DRAFT_STATUS_BADGE } from '../lib/status-badge';
import type { SharedDraftRevisionDto } from '../types';

interface SharedDraftsCardProps {
  childId: number;
}

/** Parent-side child overview card: the latest shared revision per document
 *  the school has shared for this child. Renders nothing while loading and
 *  nothing when there are none, matching `SchoolIepsCard`'s empty-safe idiom. */
export function SharedDraftsCard({ childId }: SharedDraftsCardProps) {
  const [drafts, setDrafts] = useState<SharedDraftRevisionDto[] | null>(null);

  useEffect(() => {
    let active = true;
    getSharedDrafts(childId)
      .then((res) => {
        if (active && res.success && res.data) setDrafts(res.data);
        else if (active) setDrafts([]);
      })
      .catch(() => {
        if (active) setDrafts([]);
      });
    return () => {
      active = false;
    };
  }, [childId]);

  if (drafts === null || drafts.length === 0) return null;

  // One row per document instance: the newest revision (the list is already
  // newest-first), so the card doesn't repeat superseded history.
  const latestByInstance = new Map<number, SharedDraftRevisionDto>();
  for (const draft of drafts) {
    if (!latestByInstance.has(draft.documentInstanceId)) latestByInstance.set(draft.documentInstanceId, draft);
  }
  const rows = [...latestByInstance.values()];

  return (
    <Card data-testid="shared-drafts-card">
      <h2 className="font-serif mb-1">Shared drafts</h2>
      <p className="mb-4 text-sm text-brand-slate-500">
        Drafts your child's school has shared for your review.
      </p>
      <ul className="divide-y divide-brand-slate-100" data-testid="shared-drafts-card-list">
        {rows.map((rev) => (
          <li key={rev.id} className="py-3">
            <Link
              to={`/children/${childId}/shared-drafts/${rev.id}`}
              className="flex items-center justify-between gap-4 hover:underline"
              data-testid={`shared-drafts-card-item-${rev.id}`}
            >
              <span className="flex flex-col">
                <span className="text-sm font-medium text-brand-slate-800">
                  {rev.documentTypeDisplayName} · Revision {rev.revisionNumber}
                </span>
                <span className="text-xs text-brand-slate-500">Shared {formatDate(rev.sharedAt)}</span>
              </span>
              <Badge variant={SHARED_DRAFT_STATUS_BADGE[rev.status]}>{rev.status}</Badge>
            </Link>
          </li>
        ))}
      </ul>
      <div className="mt-4">
        <Link
          to={`/children/${childId}/shared-drafts`}
          className="text-sm text-brand-teal-600 hover:underline"
          data-testid="shared-drafts-card-view-all"
        >
          View all shared drafts
        </Link>
      </div>
    </Card>
  );
}
