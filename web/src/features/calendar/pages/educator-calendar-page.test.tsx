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

  it('shows a keyboard-navigable grid where arrow keys move the active day', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('IEP check-in');

    const firstDay = screen.getByTestId('calendar-day-2026-09-15');
    firstDay.focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByTestId('calendar-day-2026-09-16')).toHaveFocus();
  });
});
