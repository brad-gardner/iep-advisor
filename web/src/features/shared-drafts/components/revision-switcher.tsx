import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { cn } from '@/lib/cn';
import { formatDate } from '@/lib/format-date';
import { getSharedDrafts } from '../api/shared-drafts-api';
import type { SharedDraftRevisionDto } from '../types';

interface RevisionSwitcherProps {
  childId: number;
  documentInstanceId: number;
  currentRevisionId: number;
}

/** Every revision ever shared for this document, oldest→newest, so a parent
 *  can jump back to what they agreed to before. Renders nothing for a
 *  single-revision document (nothing to switch between) or while loading. */
export function RevisionSwitcher({ childId, documentInstanceId, currentRevisionId }: RevisionSwitcherProps) {
  const [siblings, setSiblings] = useState<SharedDraftRevisionDto[] | null>(null);

  useEffect(() => {
    let active = true;
    getSharedDrafts(childId)
      .then((res) => {
        if (!active || !res.success || !res.data) return;
        const forThisDocument = res.data
          .filter((r) => r.documentInstanceId === documentInstanceId)
          .sort((a, b) => a.revisionNumber - b.revisionNumber);
        setSiblings(forThisDocument);
      })
      .catch(() => {
        // Non-critical: the switcher just doesn't render.
      });
    return () => {
      active = false;
    };
  }, [childId, documentInstanceId]);

  if (!siblings || siblings.length <= 1) return null;

  return (
    <nav aria-label="Revision history" data-testid="revision-switcher">
      <ul className="flex flex-wrap gap-2">
        {siblings.map((rev) => {
          const isCurrent = rev.id === currentRevisionId;
          return (
            <li key={rev.id}>
              <Link
                to={`/children/${childId}/shared-drafts/${rev.id}`}
                aria-current={isCurrent ? 'page' : undefined}
                data-testid={`revision-switcher-item-${rev.id}`}
                className={cn(
                  'inline-flex items-center rounded-badge border px-2.5 py-1 text-xs font-medium transition-colors',
                  isCurrent
                    ? 'border-brand-teal-300 bg-brand-teal-50 text-brand-teal-700'
                    : 'border-brand-slate-200 text-brand-slate-600 hover:bg-brand-slate-50'
                )}
              >
                Revision {rev.revisionNumber} · {formatDate(rev.sharedAt)}
                {rev.status !== 'Active' ? ` (${rev.status})` : ''}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
