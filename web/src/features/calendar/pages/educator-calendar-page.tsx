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

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listCalendarItems({ from: rangeFromIso, to: rangeToIso });
        if (!active) return;
        if (response.success && response.data) setItems(response.data);
        else setError(response.message ?? 'Could not load the calendar');
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load the calendar'));
      }
    })();
    return () => {
      active = false;
    };
  }, [rangeFromIso, rangeToIso]);

  const visibleItems = selectedIso
    ? (items ?? []).filter((item) => item.date.slice(0, 10) === selectedIso)
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
          <Notice variant="error" title={error} />
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
            // Cheap refresh: refetch the current range so the new meeting appears.
            setItems(null);
            listCalendarItems({ from: rangeFromIso, to: rangeToIso })
              .then((response) => {
                if (response.success && response.data) setItems(response.data);
              })
              .catch(() => undefined);
          }}
        />
      )}

      <MeetingDrawer
        open={selectedMeeting !== null}
        meeting={selectedMeeting}
        onClose={() => setSelectedMeeting(null)}
        onUpdated={(updated) => {
          setSelectedMeeting(updated);
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
