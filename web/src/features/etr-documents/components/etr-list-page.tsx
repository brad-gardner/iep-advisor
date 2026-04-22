import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { FileSearch, Users } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
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
    <div className="space-y-6">
      <div>
        <h1 className="font-serif">Evaluations (ETRs)</h1>
        <p className="mt-1 text-sm text-brand-slate-400 max-w-2xl">
          An Evaluation Team Report (ETR) is the school's written evaluation that
          determines whether your child qualifies for special education and what
          supports they need. Review past evaluations and prepare for upcoming
          meetings here.
        </p>
      </div>

      {loading && (
        <div className="flex justify-center py-12" data-testid="etr-list-loading">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        </div>
      )}

      {!loading && error && (
        <Card className="text-center py-8" data-testid="etr-list-error">
          <p className="text-brand-red mb-3">{error}</p>
          <Button onClick={refresh} variant="secondary">
            Try again
          </Button>
        </Card>
      )}

      {!loading && !error && groups.length === 0 && (
        <Card className="text-center py-12" data-testid="etr-list-empty">
          <FileSearch
            className="w-12 h-12 mx-auto text-brand-slate-300 mb-3"
            strokeWidth={1.8}
            aria-hidden="true"
          />
          <p className="text-brand-slate-400 mb-4">
            No evaluations yet. Open a child's profile to add or upload an ETR.
          </p>
          <Link to="/children">
            <Button data-testid="etr-list-empty-children-link">
              <Users className="w-4 h-4 mr-1.5" strokeWidth={1.8} aria-hidden="true" />
              Go to My Children
            </Button>
          </Link>
        </Card>
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
    </div>
  );
}
