import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { CalendarDays } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, type TableColumn } from '@/components/ui/table';
import { apiErrorMessage } from '@/lib/api-error';
import { meetingStatusLabel, meetingTypeLabel } from '@/lib/meeting-labels';
import { listStudentMeetings } from '../api/meetings-api';
import { formatMeetingWhen } from '../lib/meeting-time';
import type { MeetingDto, MeetingStatus } from '../types';
import { MeetingDrawer } from './meeting-drawer';
import { ScheduleMeetingModal } from './schedule-meeting-modal';

// A server-provided message is already resolved text and is shown as-is;
// the generic fallback is translated at RENDER time (below), not stored
// pre-translated here, so the mount effect never needs `t` in its
// dependency array (same idiom as `useHome`/`MeetingRsvpPage`'s `LoadError`).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

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
  const { t } = useTranslation(['meetings-staff', 'common']);
  const [meetings, setMeetings] = useState<MeetingDto[] | null>(null);
  const [error, setError] = useState<LoadError | null>(null);
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
          setError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded (see `use-home.ts`): re-running this fetch on
    // a plain language switch would be wasteful.
  }, [studentId, retryToken]);

  const displayError = error ? (error.kind === 'server' ? error.message : t('studentMeetingsCard.loadFailed')) : null;

  const upcoming = (meetings ?? [])
    .filter(isUpcoming)
    .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime());
  const history = (meetings ?? [])
    .filter((m) => !isUpcoming(m))
    .sort((a, b) => new Date(b.startsAtUtc).getTime() - new Date(a.startsAtUtc).getTime());

  const columns: TableColumn<MeetingDto>[] = [
    {
      key: 'title',
      header: t('studentMeetingsCard.meetingColumnHeader'),
      cell: (m) => m.title || meetingTypeLabel(m.type),
      sortValue: (m) => m.title || meetingTypeLabel(m.type),
    },
    {
      key: 'when',
      header: t('studentMeetingsCard.whenColumnHeader'),
      cell: (m) => formatMeetingWhen(m.startsAtUtc, m.durationMinutes),
      sortValue: (m) => m.startsAtUtc,
    },
    {
      key: 'status',
      header: t('studentMeetingsCard.statusColumnHeader'),
      cell: (m) => <Badge variant={statusBadgeVariant[m.status]}>{meetingStatusLabel(m.status)}</Badge>,
      sortValue: (m) => m.status,
    },
  ];

  const rowActions = (meeting: MeetingDto) => [
    {
      label: t('studentMeetingsCard.viewDetailsAction'),
      onSelect: () => setSelected(meeting),
      'data-testid': `meeting-view-${meeting.id}`,
    },
  ];

  return (
    <Card data-testid="student-meetings-card">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="font-serif text-base text-brand-slate-800">{t('studentMeetingsCard.heading')}</h2>
        <Button variant="secondary" size="sm" onClick={() => setScheduleOpen(true)} data-testid="schedule-meeting-open">
          {t('studentMeetingsCard.scheduleButton')}
        </Button>
      </div>

      {displayError && (
        <div role="alert">
          <Notice variant="error" title={displayError}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((n) => n + 1)}>
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {!displayError && meetings === null && (
        <div className="space-y-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {!displayError && meetings !== null && (
        <div className="space-y-6">
          <div>
            <h3 className="mb-2 text-xs font-medium uppercase tracking-wide text-brand-slate-500">
              {t('studentMeetingsCard.upcomingHeading')}
            </h3>
            <Table
              label={t('studentMeetingsCard.upcomingLabel')}
              columns={columns}
              rows={upcoming}
              rowKey={(m) => m.id}
              rowActions={rowActions}
              rowActionLabel={(m) => m.title || meetingTypeLabel(m.type)}
              data-testid="student-meetings-upcoming"
              empty={
                <EmptyState
                  icon={CalendarDays}
                  title={t('studentMeetingsCard.noUpcomingTitle')}
                  description={t('studentMeetingsCard.noUpcomingDescription')}
                />
              }
            />
          </div>

          {history.length > 0 && (
            <div>
              <h3 className="mb-2 text-xs font-medium uppercase tracking-wide text-brand-slate-500">
                {t('studentMeetingsCard.historyHeading')}
              </h3>
              <Table
                label={t('studentMeetingsCard.historyLabel')}
                columns={columns}
                rows={history}
                rowKey={(m) => m.id}
                rowActions={rowActions}
                rowActionLabel={(m) => m.title || meetingTypeLabel(m.type)}
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
