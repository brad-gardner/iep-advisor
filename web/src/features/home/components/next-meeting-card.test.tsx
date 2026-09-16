import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import { NextMeetingCard } from './next-meeting-card';
import { daysUntilFromNow } from '../lib/days-until';
import type { HomeMeetingDto } from '../types';

// Every date below is built from local wall-clock components (`new Date(y, m,
// d, h)`), not a raw UTC ISO literal, so the assertions hold regardless of the
// host machine's time zone — `daysUntilFromNow` itself compares LOCAL calendar
// days (a countdown should read "Tomorrow" in the viewer's own day, not UTC's).

function makeMeeting(overrides: Partial<HomeMeetingDto> = {}): HomeMeetingDto {
  return {
    id: 1,
    title: 'Annual review',
    type: 'AnnualReview',
    startsAtUtc: new Date(2026, 8, 21, 12, 0).toISOString(),
    timeZoneId: 'America/New_York',
    durationMinutes: 60,
    studentId: 10,
    studentName: 'Ada Lovelace',
    myInviteStatus: null,
    status: 'Scheduled',
    ...overrides,
  };
}

describe('daysUntilFromNow', () => {
  it('computes a calendar-day difference in the viewer\'s local time, not a fractional/UTC one', () => {
    const now = new Date(2026, 8, 16, 23, 0);
    expect(daysUntilFromNow(new Date(2026, 8, 17, 1, 0).toISOString(), now)).toBe(1);
    expect(daysUntilFromNow(new Date(2026, 8, 16, 0, 0).toISOString(), now)).toBe(0);
    expect(daysUntilFromNow(new Date(2026, 8, 21, 15, 0).toISOString(), now)).toBe(5);
  });
});

describe('NextMeetingCard countdown', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date(2026, 8, 16, 12, 0));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows no countdown badge when showCountdown is omitted (student variant)', () => {
    render(<NextMeetingCard meeting={makeMeeting()} />);
    expect(screen.queryByText(/In \d+ days|Today|Tomorrow/)).not.toBeInTheDocument();
  });

  it('derives the countdown from startsAtUtc rather than trusting a static prop', () => {
    render(
      <NextMeetingCard
        meeting={makeMeeting({ startsAtUtc: new Date(2026, 8, 21, 12, 0).toISOString() })}
        showCountdown
      />
    );
    expect(screen.getByText('In 5 days')).toBeInTheDocument();
  });

  it('re-derives the countdown on a coarse interval as the clock advances past a day boundary', () => {
    render(
      <NextMeetingCard
        meeting={makeMeeting({ startsAtUtc: new Date(2026, 8, 17, 1, 0).toISOString() })}
        showCountdown
      />
    );
    expect(screen.getByText('Tomorrow')).toBeInTheDocument();

    // Advance the clock across the day boundary without remounting the card —
    // simulates a tab left open overnight.
    act(() => {
      vi.setSystemTime(new Date(2026, 8, 17, 2, 0));
      vi.advanceTimersByTime(60_000);
    });

    expect(screen.getByText('Today')).toBeInTheDocument();
    expect(screen.queryByText('Tomorrow')).not.toBeInTheDocument();
  });

  it('clears its refresh interval on unmount', () => {
    const clearIntervalSpy = vi.spyOn(window, 'clearInterval');
    const { unmount } = render(<NextMeetingCard meeting={makeMeeting()} showCountdown />);
    unmount();
    expect(clearIntervalSpy).toHaveBeenCalled();
  });
});
