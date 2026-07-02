import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { ORG_ROLE } from '@/features/educator/types';
import { useEducatorProfile } from '@/features/educator/hooks/use-educator-profile';
import { getAuditLog } from '../api/district-api';
import { AuditLogFilters } from '../components/audit-log-filters';
import { AuditLogRow } from '../components/audit-log-row';
import type { AuditLogEntry, AuditLogFilters as AuditLogFiltersValue } from '../types';

const PAGE_SIZE = 25;

// Read-only FERPA access-audit viewer for both admin tiers. DistrictAdmin sees
// district-wide actor history; SchoolAdmin sees their own school's actors
// (server-scoped). Keyset ("Load more") paging — offset paging would drift as
// the audit worker appends rows.
export function DistrictAuditLogPage() {
  const { profile, isLoading: profileLoading } = useEducatorProfile();
  const isAdmin =
    profile?.orgRoleId === ORG_ROLE.DistrictAdmin ||
    profile?.orgRoleId === ORG_ROLE.SchoolAdmin;

  const [filters, setFilters] = useState<AuditLogFiltersValue>({});
  const [entries, setEntries] = useState<AuditLogEntry[]>([]);
  const [nextCursor, setNextCursor] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [loadFailed, setLoadFailed] = useState(false);

  // A monotonically increasing "which filter set is current" token. Both the
  // page-one effect and the (uncancelled) "Load more" fetch capture it and refuse
  // to commit a stale response, so a slow load-more can never append the previous
  // filter's rows onto a newly-filtered list — a real correctness concern for an
  // access-audit view.
  const generationRef = useRef(0);
  const hasFilters = Object.keys(filters).length > 0;

  // Load (or reload) page one whenever the filter set changes. The cancellation
  // flag guards the effect's own commits; every setState runs inside the async
  // callback, never synchronously in the effect body.
  useEffect(() => {
    if (!isAdmin) return;
    let active = true;
    (async () => {
      try {
        const response = await getAuditLog({ ...filters, pageSize: PAGE_SIZE });
        if (!active) return;
        if (response.success && response.data) {
          setEntries(response.data.entries);
          setNextCursor(response.data.nextCursor);
          setLoadFailed(false);
        } else {
          setEntries([]);
          setNextCursor(null);
          setLoadFailed(true);
        }
      } catch {
        if (active) {
          setEntries([]);
          setNextCursor(null);
          setLoadFailed(true);
        }
      } finally {
        if (active) setIsLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [isAdmin, filters]);

  // A filter change (or retry) starts a new generation, resets the accumulated
  // page, and shows the spinner; the effect above then refetches from cursor=null.
  // Runs in an event handler, so the synchronous setStates are allowed.
  const applyFilters = (next: AuditLogFiltersValue) => {
    generationRef.current += 1;
    setEntries([]);
    setNextCursor(null);
    setLoadFailed(false);
    setIsLoading(true);
    setFilters(next);
  };

  const handleLoadMore = async () => {
    if (nextCursor == null) return;
    const generation = generationRef.current;
    setIsLoadingMore(true);
    try {
      const response = await getAuditLog({
        ...filters,
        cursor: nextCursor,
        pageSize: PAGE_SIZE,
      });
      // Drop the response if the filter set changed while it was in flight.
      if (generationRef.current !== generation) return;
      if (response.success && response.data) {
        const page = response.data;
        setEntries((prev) => [...prev, ...page.entries]);
        setNextCursor(page.nextCursor);
      }
    } catch {
      // Keep the current entries and cursor so the user can retry.
    } finally {
      if (generationRef.current === generation) setIsLoadingMore(false);
    }
  };

  if (profileLoading) {
    return (
      <div className="flex justify-center py-12" data-testid="audit-log-page" role="status">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
        <span className="sr-only">Loading…</span>
      </div>
    );
  }

  if (!isAdmin) {
    return (
      <div className="space-y-6" data-testid="audit-log-page">
        <h1 className="font-serif">Activity log</h1>
        <Notice variant="warning" title="Access restricted">
          The activity log is available to district and school administrators only.
        </Notice>
      </div>
    );
  }

  return (
    <div className="space-y-6" data-testid="audit-log-page">
      <div>
        <h1 className="font-serif">Activity log</h1>
        <p className="mt-1 text-sm text-brand-slate-500">
          Who accessed which student records, and when.
        </p>
      </div>

      <AuditLogFilters onChange={applyFilters} />

      {isLoading ? (
        <div className="flex justify-center py-12" role="status">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-brand-teal-500" />
          <span className="sr-only">Loading activity…</span>
        </div>
      ) : loadFailed ? (
        <Notice variant="error" title="Couldn't load activity">
          Something went wrong loading the activity log.{' '}
          <button
            type="button"
            onClick={() => applyFilters({ ...filters })}
            className="text-brand-teal-600 hover:underline"
            data-testid="audit-log-retry"
          >
            Try again
          </button>
        </Notice>
      ) : entries.length === 0 ? (
        <Card data-testid="audit-log-empty">
          <p className="text-center text-sm text-brand-slate-500">
            {hasFilters
              ? 'No activity matches these filters.'
              : 'No activity recorded yet.'}
          </p>
        </Card>
      ) : (
        <>
          <Card className="divide-y divide-brand-slate-100 p-0">
            {entries.map((entry) => (
              <AuditLogRow key={entry.id} entry={entry} />
            ))}
          </Card>

          {nextCursor != null && (
            <div className="flex justify-center">
              <Button
                variant="secondary"
                onClick={handleLoadMore}
                disabled={isLoadingMore}
                data-testid="audit-log-load-more"
              >
                {isLoadingMore ? 'Loading…' : 'Load more'}
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
