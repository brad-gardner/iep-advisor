import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CalendarMonthGrid } from './calendar-month-grid';
import type { CalendarDay } from '../hooks/use-calendar-range';

function makeDays(): CalendarDay[] {
  const days: CalendarDay[] = [];
  for (let d = 1; d <= 7; d += 1) {
    days.push({
      date: new Date(2026, 9, d), // October 2026
      iso: `2026-10-${String(d).padStart(2, '0')}`,
      inCurrentMonth: true,
      isToday: d === 3,
    });
  }
  return days;
}

describe('CalendarMonthGrid', () => {
  it("falls back the tab-stoppable cell to today when the selected day isn't part of the visible days", () => {
    const days = makeDays();
    render(<CalendarMonthGrid days={days} items={[]} selectedIso="2026-09-15" onSelectDay={() => {}} />);

    // A stale selection from a month the user has since navigated away from
    // must not pin roving focus to the grid's first (often dimmed) cell.
    expect(screen.getByTestId('calendar-day-2026-10-03')).toHaveAttribute('tabindex', '0');
    expect(screen.getByTestId('calendar-day-2026-10-01')).toHaveAttribute('tabindex', '-1');
  });

  it('lands the tab-stoppable cell on a selection that is part of the visible days', () => {
    const days = makeDays();
    render(<CalendarMonthGrid days={days} items={[]} selectedIso="2026-10-05" onSelectDay={() => {}} />);

    expect(screen.getByTestId('calendar-day-2026-10-05')).toHaveAttribute('tabindex', '0');
  });

  it('wraps each day in a role="gridcell" container carrying aria-selected, leaving the inner button role native', () => {
    const days = makeDays();
    render(<CalendarMonthGrid days={days} items={[]} selectedIso="2026-10-03" onSelectDay={() => {}} />);

    const button = screen.getByTestId('calendar-day-2026-10-03');
    expect(button.tagName).toBe('BUTTON');
    expect(button).not.toHaveAttribute('role');

    const wrapper = button.closest('[role="gridcell"]');
    expect(wrapper).not.toBeNull();
    expect(wrapper).toHaveAttribute('aria-selected', 'true');

    const unselectedWrapper = screen.getByTestId('calendar-day-2026-10-01').closest('[role="gridcell"]');
    expect(unselectedWrapper).toHaveAttribute('aria-selected', 'false');
  });

  it('builds an accessible name from the full date, today status, and item count, hiding the visible digits from AT', () => {
    const days = makeDays();
    const items = [
      { kind: 'Meeting' as const, date: '2026-10-03T15:00:00.000Z' },
      { kind: 'Meeting' as const, date: '2026-10-03T18:00:00.000Z' },
    ];
    render(<CalendarMonthGrid days={days} items={items} selectedIso={null} onSelectDay={() => {}} />);

    const button = screen.getByTestId('calendar-day-2026-10-03');
    expect(button).toHaveAccessibleName(/Saturday, October 3, 2026/);
    expect(button).toHaveAccessibleName(/today/);
    expect(button).toHaveAccessibleName(/2 items/);
    // The visible day-of-month digit is decorative once the label exists.
    expect(button.querySelector('[aria-hidden="true"]')).not.toBeNull();
  });
});
