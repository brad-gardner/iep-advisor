import { Link } from 'react-router-dom';
import { CalendarClock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { EmptyState } from '@/components/ui/empty-state';
import { formatMeetingWhen } from '@/features/meetings/lib/meeting-time';
import { MEETING_TYPE_LABELS } from '@/features/meetings/types';
import { ObligationStatusChip } from '@/features/obligations/components/obligation-status-chip';
import { OBLIGATION_KIND_LABELS } from '@/features/obligations/types';
import { formatDate } from '@/lib/format-date';
import type { CalendarItemDto } from '../types';
import type { MeetingDto } from '@/features/meetings/types';

interface CalendarAgendaListProps {
  items: CalendarItemDto[];
  onSelectMeeting: (meeting: MeetingDto) => void;
}

/** Chronological list of the visible range's meetings and obligations —
 * meetings open the meeting drawer, obligations link to the student. */
export function CalendarAgendaList({ items, onSelectMeeting }: CalendarAgendaListProps) {
  const sorted = [...items].sort((a, b) => a.date.localeCompare(b.date));

  if (sorted.length === 0) {
    return (
      <EmptyState
        icon={CalendarClock}
        title="Nothing on the calendar"
        description="Meetings and upcoming deadlines in this range will show up here."
        data-testid="calendar-agenda-empty"
      />
    );
  }

  return (
    <ul className="divide-y divide-brand-slate-100 rounded-card border border-brand-slate-200" data-testid="calendar-agenda-list">
      {sorted.map((item) => {
        if (item.kind === 'Meeting' && item.meeting) {
          const meeting = item.meeting;
          return (
            <li key={`meeting-${meeting.id}`}>
              <button
                type="button"
                onClick={() => onSelectMeeting(meeting)}
                className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition-colors hover:bg-brand-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-brand-teal-400"
                data-testid={`calendar-item-meeting-${meeting.id}`}
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-brand-slate-800">
                    {meeting.title || MEETING_TYPE_LABELS[meeting.type]}
                  </p>
                  <p className="text-xs text-brand-slate-500">
                    {meeting.studentName} · {formatMeetingWhen(meeting.startsAtUtc, meeting.durationMinutes)}
                  </p>
                </div>
                <Badge variant="neutral">Meeting</Badge>
              </button>
            </li>
          );
        }

        if (item.kind === 'Obligation' && item.obligation) {
          const obligation = item.obligation;
          return (
            <li key={`obligation-${obligation.schoolStudentId}-${obligation.kind}`}>
              <Link
                to={`/educator/students/${obligation.schoolStudentId}`}
                className="flex items-center justify-between gap-3 px-4 py-3 transition-colors hover:bg-brand-slate-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-brand-teal-400"
                data-testid={`calendar-item-obligation-${obligation.schoolStudentId}-${obligation.kind}`}
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-brand-slate-800">
                    {OBLIGATION_KIND_LABELS[obligation.kind]} — {obligation.studentName}
                  </p>
                  <p className="text-xs text-brand-slate-500">Due {formatDate(obligation.dueDate)}</p>
                </div>
                <ObligationStatusChip status={obligation.status} />
              </Link>
            </li>
          );
        }

        return null;
      })}
    </ul>
  );
}
