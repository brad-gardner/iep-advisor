import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ToastProvider } from '@/components/ui/toast';
import { makeMeeting } from '@/features/meetings/test/fixtures';
import type { CalendarItemDto } from '../types';

const calendarApi = vi.hoisted(() => ({ listCalendarItems: vi.fn() }));
vi.mock('../api/calendar-api', () => calendarApi);

const educatorApi = vi.hoisted(() => ({
  searchStudents: vi.fn(),
  getEligibleTeamStaff: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/educator/api/educator-api', () => educatorApi);

const meetingsApi = vi.hoisted(() => ({
  rsvpToMeeting: vi.fn(),
  createMeeting: vi.fn(),
  getDefaultParticipants: vi.fn().mockResolvedValue({ success: true, data: [] }),
}));
vi.mock('@/features/meetings/api/meetings-api', () => meetingsApi);

import { EducatorCalendarPage } from './educator-calendar-page';

function renderPage() {
  return render(
    <ToastProvider>
      <MemoryRouter>
        <EducatorCalendarPage />
      </MemoryRouter>
    </ToastProvider>
  );
}

const meetingItem: CalendarItemDto = {
  kind: 'Meeting',
  date: '2026-09-15T15:00:00.000Z',
  meeting: makeMeeting({ id: 200, title: 'IEP check-in', startsAtUtc: '2026-09-15T15:00:00.000Z' }),
};

const obligationItem: CalendarItemDto = {
  kind: 'Obligation',
  date: '2026-09-20',
  obligation: {
    kind: 'AnnualReview',
    dueDate: '2026-09-20',
    status: 'DueSoon',
    sourceLabel: 'from IEP date',
    ownerUserId: 7,
    ownerName: 'Casey Manager',
    schoolStudentId: 11,
    studentName: 'Alan Turing',
    daysUntilDue: 5,
    ruleProfile: 'OH',
  },
};

describe('EducatorCalendarPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    calendarApi.listCalendarItems.mockResolvedValue({ success: true, data: [meetingItem, obligationItem] });
  });

  it('loads the current month range and renders meeting + obligation items', async () => {
    renderPage();
    expect(await screen.findByText('IEP check-in')).toBeInTheDocument();
    expect(screen.getByText(/Annual review — Alan Turing/)).toBeInTheDocument();
    expect(calendarApi.listCalendarItems).toHaveBeenCalledWith(
      expect.objectContaining({ from: expect.any(String), to: expect.any(String) })
    );
  });

  it('navigates to the next and previous month, refetching the range each time', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('IEP check-in');
    const initialLabel = screen.getByRole('heading', { level: 2 }).textContent;
    const initialCalls = calendarApi.listCalendarItems.mock.calls.length;

    await user.click(screen.getByTestId('calendar-next-month'));
    await waitFor(() => expect(calendarApi.listCalendarItems.mock.calls.length).toBeGreaterThan(initialCalls));
    expect(screen.getByRole('heading', { level: 2 }).textContent).not.toBe(initialLabel);

    await user.click(screen.getByTestId('calendar-prev-month'));
    await waitFor(() => expect(screen.getByRole('heading', { level: 2 }).textContent).toBe(initialLabel));

    await user.click(screen.getByTestId('calendar-today'));
    await waitFor(() => expect(screen.getByRole('heading', { level: 2 })).toBeInTheDocument());
  });

  it('filters the agenda to a selected day and clears on a second click', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('IEP check-in');

    await user.click(screen.getByTestId('calendar-day-2026-09-15'));
    expect(screen.getByText('IEP check-in')).toBeInTheDocument();
    expect(screen.queryByText(/Annual review — Alan Turing/)).not.toBeInTheDocument();

    await user.click(screen.getByTestId('calendar-day-2026-09-15'));
    expect(screen.getByText(/Annual review — Alan Turing/)).toBeInTheDocument();
  });

  it('opens the meeting drawer when an agenda meeting is clicked', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('IEP check-in');

    await user.click(screen.getByTestId('calendar-item-meeting-200'));
    expect(await screen.findByTestId('meeting-drawer')).toHaveTextContent('IEP check-in');
  });

  it('links an obligation item to its student', async () => {
    renderPage();
    await screen.findByText('IEP check-in');
    const link = screen.getByTestId('calendar-item-obligation-11-AnnualReview');
    expect(link).toHaveAttribute('href', '/educator/students/11');
  });

  it('opens the student picker, then the schedule modal, when Schedule meeting is clicked', async () => {
    const user = userEvent.setup();
    educatorApi.searchStudents.mockResolvedValue({
      success: true,
      data: { items: [{ id: 5, firstName: 'Ada', lastName: 'Lovelace' }], total: 1, page: 1, pageSize: 25 },
    });
    renderPage();
    await screen.findByText('IEP check-in');

    await user.click(screen.getByTestId('calendar-schedule-meeting'));
    await user.click(await screen.findByTestId('student-picker-option-5'));

    expect(await screen.findByTestId('schedule-meeting-modal')).toHaveTextContent('Ada Lovelace');
  });

  it('does not let a slow post-schedule refetch overwrite a month the user has since navigated to', async () => {
    const user = userEvent.setup();
    const octoberItem: CalendarItemDto = {
      kind: 'Meeting',
      date: '2026-10-05T15:00:00.000Z',
      meeting: makeMeeting({ id: 300, title: 'October meeting', startsAtUtc: '2026-10-05T15:00:00.000Z' }),
    };
    let resolveSlowRefetch: (value: unknown) => void = () => {};
    calendarApi.listCalendarItems
      .mockResolvedValueOnce({ success: true, data: [meetingItem, obligationItem] }) // initial mount
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveSlowRefetch = resolve;
          })
      ) // post-schedule refetch of the (still) current month — deliberately slow
      .mockResolvedValueOnce({ success: true, data: [octoberItem] }); // the user's own Next-month fetch — fast

    educatorApi.searchStudents.mockResolvedValue({
      success: true,
      data: { items: [{ id: 5, firstName: 'Ada', lastName: 'Lovelace' }], total: 1, page: 1, pageSize: 25 },
    });
    meetingsApi.createMeeting.mockResolvedValue({ success: true, data: makeMeeting({ id: 999 }) });

    renderPage();
    await screen.findByText('IEP check-in');

    await user.click(screen.getByTestId('calendar-schedule-meeting'));
    await user.click(await screen.findByTestId('student-picker-option-5'));
    await screen.findByTestId('schedule-meeting-modal');

    await user.type(screen.getByLabelText('Date'), '2026-09-20');
    await user.type(screen.getByLabelText('Time'), '10:00');
    await waitFor(() => expect(screen.getByTestId('schedule-meeting-submit')).not.toBeDisabled());
    await user.click(screen.getByTestId('schedule-meeting-submit'));

    // The post-schedule refetch (same month, slow) is now in flight.
    await waitFor(() => expect(calendarApi.listCalendarItems).toHaveBeenCalledTimes(2));

    // Before it resolves, the user navigates to next month — a fast, independent fetch.
    await user.click(screen.getByTestId('calendar-next-month'));
    await waitFor(() => expect(screen.getByText('October meeting')).toBeInTheDocument());

    // The slow refetch finally resolves — it must not clobber the now-current October view.
    resolveSlowRefetch({ success: true, data: [meetingItem, obligationItem] });
    await waitFor(() => expect(calendarApi.listCalendarItems).toHaveBeenCalledTimes(3));
    expect(screen.getByText('October meeting')).toBeInTheDocument();
    expect(screen.queryByText('IEP check-in')).not.toBeInTheDocument();
  });

  it('does not reopen the drawer when a mutation resolves after it was closed', async () => {
    const user = userEvent.setup();
    let resolveRsvp: (value: unknown) => void = () => {};
    meetingsApi.rsvpToMeeting.mockReturnValue(
      new Promise((resolve) => {
        resolveRsvp = resolve;
      })
    );
    calendarApi.listCalendarItems.mockResolvedValue({
      success: true,
      data: [{ ...meetingItem, meeting: makeMeeting({ id: 200, title: 'IEP check-in', myInviteStatus: 'Pending' }) }],
    });
    renderPage();
    await screen.findByText('IEP check-in');

    await user.click(screen.getByTestId('calendar-item-meeting-200'));
    await screen.findByTestId('meeting-drawer');
    await user.click(screen.getByTestId('meeting-rsvp-accept'));

    // Close the drawer while the RSVP request is still in flight.
    await user.click(screen.getByLabelText(/close/i));
    await waitFor(() => expect(screen.queryByTestId('meeting-drawer')).not.toBeInTheDocument());

    // The RSVP response arrives after the close — it must not reopen the drawer.
    resolveRsvp({ success: true, data: makeMeeting({ id: 200, title: 'IEP check-in', myInviteStatus: 'Accepted' }) });
    await waitFor(() => expect(meetingsApi.rsvpToMeeting).toHaveBeenCalled());
    expect(screen.queryByTestId('meeting-drawer')).not.toBeInTheDocument();
  });

  it('shows a keyboard-navigable grid where arrow keys move the active day', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('IEP check-in');

    const firstDay = screen.getByTestId('calendar-day-2026-09-15');
    firstDay.focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByTestId('calendar-day-2026-09-16')).toHaveFocus();
  });

  it('gives each day cell a full-date accessible name and keeps native button semantics', async () => {
    renderPage();
    await screen.findByText('IEP check-in');

    const day = screen.getByTestId('calendar-day-2026-09-15');
    expect(day.tagName).toBe('BUTTON');
    expect(day).toHaveAccessibleName(/September 15, 2026/);
    expect(day).toHaveAccessibleName(/1 item/);
    // role="gridcell" lives on a wrapper, not on the button itself.
    expect(day.closest('[role="gridcell"]')).not.toBeNull();
    expect(day).not.toHaveAttribute('role', 'gridcell');
  });

  it('clears the selected-day filter when navigating to a different month', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('IEP check-in');

    await user.click(screen.getByTestId('calendar-day-2026-09-15'));
    expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent('Selected day');

    await user.click(screen.getByTestId('calendar-next-month'));
    await waitFor(() => expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent('This month'));
  });
});
