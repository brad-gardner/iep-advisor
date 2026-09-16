import { useEffect, useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Skeleton } from '@/components/ui/skeleton';
import { apiErrorMessage } from '@/lib/api-error';
import { MeetingDrawer } from '@/features/meetings/components/meeting-drawer';
import { ScheduleMeetingModal } from '@/features/meetings/components/schedule-meeting-modal';
import type { MeetingDto } from '@/features/meetings/types';
import type { SchoolStudent } from '@/features/educator/types';
import { listCalendarItems } from '../api/calendar-api';
import { CalendarAgendaList } from '../components/calendar-agenda-list';
import { CalendarMonthGrid } from '../components/calendar-month-grid';
import { StudentPickerModal } from '../components/student-picker-modal';
import { useCalendarRange } from '../hooks/use-calendar-range';
import { calendarItemLocalDateIso } from '../lib/calendar-item-date';
import type { CalendarItemDto } from '../types';

export function EducatorCalendarPage() {
  const { monthLabel, days, rangeFromIso, rangeToIso, goToPreviousMonth, goToNextMonth, goToToday } =
    useCalendarRange();
  const [items, setItems] = useState<CalendarItemDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectedIso, setSelectedIso] = useState<string | null>(null);
  const [selectedMeeting, setSelectedMeeting] = useState<MeetingDto | null>(null);
  const [pickerOpen, setPickerOpen] = useState(false);
  const [schedulingStudent, setSchedulingStudent] = useState<SchoolStudent | null>(null);
  // Bumped instead of firing a second, unguarded fetch (see the "Schedule
  // meeting" `onSaved` below) so every calendar refetch — range navigation or
  // a post-schedule refresh — goes through the one effect below and its
  // `active` staleness guard. Without this, a slow refetch for a range the
  // user has since navigated away from could land after (and overwrite) a
  // faster fetch for the range they're now looking at.
  const [refreshToken, setRefreshToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listCalendarItems({ from: rangeFromIso, to: rangeToIso });
        if (!active) return;
        if (response.success && response.data) {
          setItems(response.data);
          setError(null);
        } else {
          setError(response.message ?? 'Could not load the calendar');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load the calendar'));
      }
    })();
    return () => {
      active = false;
    };
  }, [rangeFromIso, rangeToIso, refreshToken]);

  // A day selected in one month can't exist in another — clear it whenever
  // the visible range changes, so the agenda doesn't get stuck rendering an
  // empty "Selected day" filter for a date that isn't in the new range.
  // Computed during render (the same "adjust state in response to a change"
  // idiom `meeting-drawer.tsx`/`schedule-meeting-form.tsx` use for the same
  // reason: no synchronous setState-in-effect, no extra render pass), keyed
  // on the range rather than `refreshToken` so a post-schedule refresh of the
  // same range doesn't discard the user's current selection.
  const [seenRange, setSeenRange] = useState(`${rangeFromIso}|${rangeToIso}`);
  const currentRange = `${rangeFromIso}|${rangeToIso}`;
  if (currentRange !== seenRange) {
    setSeenRange(currentRange);
    setSelectedIso(null);
  }

  const visibleItems = selectedIso
    ? (items ?? []).filter((item) => calendarItemLocalDateIso(item) === selectedIso)
    : (items ?? []);

  return (
    <PageLayout
      title="Calendar"
      actions={
        <Button onClick={() => setPickerOpen(true)} data-testid="calendar-schedule-meeting">
          Schedule meeting
        </Button>
      }
    >
      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button
              size="sm"
              variant="secondary"
              onClick={() => setRefreshToken((t) => t + 1)}
              data-testid="calendar-retry"
            >
              Try again
            </Button>
          </Notice>
        </div>
      )}

      <div className="flex items-center justify-between gap-3">
        <h2 className="font-serif text-lg text-brand-slate-800">{monthLabel}</h2>
        <div className="flex items-center gap-1">
          <Button
            variant="ghost"
            size="sm"
            onClick={goToPreviousMonth}
            aria-label="Previous month"
            data-testid="calendar-prev-month"
          >
            <ChevronLeft className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          </Button>
          <Button variant="ghost" size="sm" onClick={goToToday} data-testid="calendar-today">
            Today
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={goToNextMonth}
            aria-label="Next month"
            data-testid="calendar-next-month"
          >
            <ChevronRight className="h-4 w-4" strokeWidth={2} aria-hidden="true" />
          </Button>
        </div>
      </div>

      {items === null ? (
        <Skeleton className="h-96 w-full" />
      ) : (
        <CalendarMonthGrid
          days={days}
          items={items}
          selectedIso={selectedIso}
          onSelectDay={(iso) => setSelectedIso((prev) => (prev === iso ? null : iso))}
        />
      )}

      <div>
        <h3 className="mb-2 text-sm font-medium text-brand-slate-800">
          {selectedIso ? 'Selected day' : 'This month'}
        </h3>
        {items === null ? (
          <div className="space-y-2">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : (
          <CalendarAgendaList items={visibleItems} onSelectMeeting={setSelectedMeeting} />
        )}
      </div>

      <StudentPickerModal
        open={pickerOpen}
        onClose={() => setPickerOpen(false)}
        onSelect={(student) => {
          setSchedulingStudent(student);
          setPickerOpen(false);
        }}
      />

      {schedulingStudent && (
        <ScheduleMeetingModal
          open
          onClose={() => setSchedulingStudent(null)}
          studentId={schedulingStudent.id}
          studentName={`${schedulingStudent.firstName} ${schedulingStudent.lastName ?? ''}`.trim()}
          onSaved={() => {
            setSchedulingStudent(null);
            // Refetch the current range so the new meeting appears — routed
            // through the range effect above (via `refreshToken`) rather than
            // a second ad hoc fetch, so it shares that effect's `active`
            // staleness guard and can never overwrite a range the user has
            // since navigated away from.
            setItems(null);
            setRefreshToken((t) => t + 1);
          }}
        />
      )}

      <MeetingDrawer
        open={selectedMeeting !== null}
        meeting={selectedMeeting}
        onClose={() => setSelectedMeeting(null)}
        onUpdated={(updated) => {
          // A mutation started before the user closed the drawer (or opened a
          // different meeting) can still resolve afterward — only reopen/update
          // the drawer if it's still showing this same meeting; the functional
          // updater reads the *current* selection, not the one captured when
          // the mutation started. The list merge below applies regardless.
          setSelectedMeeting((prev) => (prev && prev.id === updated.id ? updated : prev));
          setItems((prev) =>
            prev?.map((item) =>
              item.kind === 'Meeting' && item.meeting?.id === updated.id
                ? { ...item, meeting: updated }
                : item
            ) ?? prev
          );
        }}
      />
    </PageLayout>
  );
}
