import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { FileSearch, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { Spinner } from '@/components/ui/spinner';
import { EmptyState } from '@/components/ui/empty-state';
import { PageLayout } from '@/components/ui/page-layout';
import { useAllEtrs } from '../hooks/use-all-etrs';
import type { EtrDocumentListItem } from '../types';
import { EtrListGroup } from './etr-list-group';

interface ChildGroup {
  childId: number;
  childFirstName: string;
  childLastName: string;
  etrs: EtrDocumentListItem[];
}

function groupByChild(etrs: EtrDocumentListItem[]): ChildGroup[] {
  const map = new Map<number, ChildGroup>();
  for (const etr of etrs) {
    const existing = map.get(etr.childId);
    if (existing) {
      existing.etrs.push(etr);
    } else {
      map.set(etr.childId, {
        childId: etr.childId,
        childFirstName: etr.childFirstName,
        childLastName: etr.childLastName,
        etrs: [etr],
      });
    }
  }
  return Array.from(map.values());
}

export function EtrListPage() {
  const { etrs, loading, error, refresh } = useAllEtrs();
  const groups = useMemo(() => groupByChild(etrs), [etrs]);

  return (
    <PageLayout
      title="Evaluations (ETRs)"
      subtitle="An Evaluation Team Report (ETR) is the school's written evaluation that determines whether your child qualifies for special education and what supports they need. Review past evaluations and prepare for upcoming meetings here."
    >
      {loading && (
        <div className="flex justify-center py-12" data-testid="etr-list-loading">
          <Spinner label="Loading evaluations…" />
        </div>
      )}

      {!loading && error && (
        <Notice variant="error" title="Couldn't load evaluations" data-testid="etr-list-error">
          <p className="mb-3">{error}</p>
          <Button onClick={refresh} variant="secondary" size="sm">
            Try again
          </Button>
        </Notice>
      )}

      {!loading && !error && groups.length === 0 && (
        <EmptyState
          icon={FileSearch}
          title="No evaluations yet."
          description="Open a child's profile to add or upload an ETR."
          action={
            <Link to="/children">
              <Button data-testid="etr-list-empty-children-link">
                <Users className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
                Go to My Children
              </Button>
            </Link>
          }
          data-testid="etr-list-empty"
        />
      )}

      {!loading && !error && groups.length > 0 && (
        <div className="space-y-4">
          {groups.map((group) => (
            <EtrListGroup
              key={group.childId}
              childId={group.childId}
              childFirstName={group.childFirstName}
              childLastName={group.childLastName}
              etrs={group.etrs}
            />
          ))}
        </div>
      )}
    </PageLayout>
  );
}
