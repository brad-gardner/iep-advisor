import { useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { listStudentObligations } from '../api/obligations-api';
import { OBLIGATION_KIND_LABELS } from '../types';
import type { ObligationDto } from '../types';
import { ObligationStatusChip } from './obligation-status-chip';

interface StudentTimelineCardProps {
  studentId: number;
  /** Opens the host's existing student-dates edit drawer; omit to hide the
   * action (e.g. for a viewer without edit permission). */
  onEditDates?: () => void;
}

/** Procedural deadlines (annual review, reevaluation, ETR) computed from the
 * student's dates, with status chips and a link to edit those dates. */
export function StudentTimelineCard({ studentId, onEditDates }: StudentTimelineCardProps) {
  const [obligations, setObligations] = useState<ObligationDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listStudentObligations(studentId);
        if (!active) return;
        if (response.success && response.data) setObligations(response.data);
        else setError(response.message ?? 'Could not load the timeline');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load the timeline'));
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId]);

  return (
    <Card data-testid="student-timeline-card">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-serif text-base text-brand-slate-800">Timeline</h2>
        {onEditDates && (
          <Button variant="ghost" size="sm" onClick={onEditDates} data-testid="timeline-edit-dates">
            Edit dates
          </Button>
        )}
      </div>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error} />
        </div>
      )}

      {!error && obligations === null && (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {!error && obligations !== null && (
        <dl className="space-y-3 text-sm" data-testid="student-timeline-list">
          {obligations.map((o) => (
            <div
              key={o.kind}
              className="flex items-center justify-between gap-3"
              data-testid={`timeline-row-${o.kind}`}
            >
              <div>
                <dt className="text-brand-slate-500">{OBLIGATION_KIND_LABELS[o.kind]}</dt>
                <dd className="text-brand-slate-800">
                  {formatDate(o.dueDate)}
                  <span className="ml-1 text-xs text-brand-slate-400">{o.sourceLabel}</span>
                </dd>
              </div>
              <ObligationStatusChip status={o.status} />
            </div>
          ))}
        </dl>
      )}
    </Card>
  );
}
