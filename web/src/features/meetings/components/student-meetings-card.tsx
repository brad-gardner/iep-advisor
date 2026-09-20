import { useEffect, useState } from 'react';
import { CalendarDays } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, type TableColumn } from '@/components/ui/table';
import { apiErrorMessage } from '@/lib/api-error';
import { listStudentMeetings } from '../api/meetings-api';
import { formatMeetingWhen } from '../lib/meeting-time';
import { MEETING_STATUS_LABELS, MEETING_TYPE_LABELS } from '../types';
import type { MeetingDto, MeetingStatus } from '../types';
import { MeetingDrawer } from './meeting-drawer';
import { ScheduleMeetingModal } from './schedule-meeting-modal';

const statusBadgeVariant: Record<MeetingStatus, 'success' | 'error' | 'warning' | 'neutral'> = {
  Proposed: 'neutral',
  Scheduled: 'success',
  Held: 'success',
  Continued: 'warning',
  Cancelled: 'error',
};

function isUpcoming(meeting: MeetingDto): boolean {
  return (
    (meeting.status === 'Scheduled' || meeting.status === 'Proposed') &&
    new Date(meeting.startsAtUtc).getTime() >= Date.now()
  );
}

interface StudentMeetingsCardProps {
  studentId: number;
  studentName?: string;
}

/** Upcoming + history meetings for a student, with schedule/view entry
 * points. Mounted on the educator student detail page. */
export function StudentMeetingsCard({ studentId, studentName }: StudentMeetingsCardProps) {
  const [meetings, setMeetings] = useState<MeetingDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [scheduleOpen, setScheduleOpen] = useState(false);
  const [selected, setSelected] = useState<MeetingDto | null>(null);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listStudentMeetings(studentId);
        if (!active) return;
        if (response.success && response.data) {
          setMeetings(response.data);
          setError(null);
        } else {
          setError(response.message ?? 'Could not load meetings');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load meetings'));
      }
    })();
    return () => {
      active = false;
    };
  }, [studentId, retryToken]);

  const upcoming = (meetings ?? [])
    .filter(isUpcoming)
    .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime());
  const history = (meetings ?? [])
    .filter((m) => !isUpcoming(m))
    .sort((a, b) => new Date(b.startsAtUtc).getTime() - new Date(a.startsAtUtc).getTime());

  const columns: TableColumn<MeetingDto>[] = [
    {
      key: 'title',
      header: 'Meeting',
      cell: (m) => m.title || MEETING_TYPE_LABELS[m.type],
      sortValue: (m) => m.title || MEETING_TYPE_LABELS[m.type],
    },
    {
      key: 'when',
      header: 'When',
      cell: (m) => formatMeetingWhen(m.startsAtUtc, m.durationMinutes),
      sortValue: (m) => m.startsAtUtc,
    },
    {
      key: 'status',
      header: 'Status',
      cell: (m) => <Badge variant={statusBadgeVariant[m.status]}>{MEETING_STATUS_LABELS[m.status]}</Badge>,
      sortValue: (m) => m.status,
    },
  ];

  const rowActions = (meeting: MeetingDto) => [
    { label: 'View details', onSelect: () => setSelected(meeting), 'data-testid': `meeting-view-${meeting.id}` },
  ];

  return (
    <Card data-testid="student-meetings-card">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-serif text-base text-brand-slate-800">Meetings</h2>
        <Button variant="secondary" size="sm" onClick={() => setScheduleOpen(true)} data-testid="schedule-meeting-open">
          Schedule meeting
        </Button>
      </div>

      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((t) => t + 1)}>
              Try again
            </Button>
          </Notice>
        </div>
      )}

      {!error && meetings === null && (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {!error && meetings !== null && (
        <div className="space-y-6">
          <div>
            <h3 className="mb-2 text-xs font-medium uppercase tracking-wide text-brand-slate-500">Upcoming</h3>
            <Table
              label="Upcoming meetings"
              columns={columns}
              rows={upcoming}
              rowKey={(m) => m.id}
              rowActions={rowActions}
              rowActionLabel={(m) => m.title || MEETING_TYPE_LABELS[m.type]}
              data-testid="student-meetings-upcoming"
              empty={
                <EmptyState
                  icon={CalendarDays}
                  title="No upcoming meetings"
                  description="Schedule an IEP meeting for this student."
                />
              }
            />
          </div>

          {history.length > 0 && (
            <div>
              <h3 className="mb-2 text-xs font-medium uppercase tracking-wide text-brand-slate-500">History</h3>
              <Table
                label="Meeting history"
                columns={columns}
                rows={history}
                rowKey={(m) => m.id}
                rowActions={rowActions}
                rowActionLabel={(m) => m.title || MEETING_TYPE_LABELS[m.type]}
                data-testid="student-meetings-history"
              />
            </div>
          )}
        </div>
      )}

      <ScheduleMeetingModal
        open={scheduleOpen}
        onClose={() => setScheduleOpen(false)}
        studentId={studentId}
        studentName={studentName}
        onSaved={(meeting) => {
          setMeetings((prev) => [meeting, ...(prev ?? [])]);
        }}
      />

      <MeetingDrawer
        open={selected !== null}
        meeting={selected}
        onClose={() => setSelected(null)}
        onUpdated={(updated) => {
          // See educator-calendar-page.tsx's `onUpdated` for why this is a
          // functional update: a mutation started before the drawer was
          // closed (or re-opened for a different meeting) can resolve after
          // the fact, and must not reopen/overwrite whatever is showing now.
          setSelected((prev) => (prev && prev.id === updated.id ? updated : prev));
          setMeetings((prev) => prev?.map((m) => (m.id === updated.id ? updated : m)) ?? prev);
        }}
      />
    </Card>
  );
}
