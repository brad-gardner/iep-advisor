import { describe, it, expect, vi } from 'vitest';
import { makeMeeting } from '@/features/meetings/test/fixtures';
import type { CalendarItemDto } from '../types';

vi.mock('@/features/meetings/lib/meeting-time', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/meetings/lib/meeting-time')>();
  // Pin the "viewer's" zone so the test is deterministic regardless of the
  // machine/CI running it.
  return { ...actual, browserTimeZone: () => 'America/Los_Angeles' };
});

import { calendarItemLocalDateIso } from './calendar-item-date';

describe('calendarItemLocalDateIso', () => {
  it("buckets a Meeting item by the viewer's local calendar day, not the raw UTC date it crosses into", () => {
    // 2026-09-15 18:00 PDT (UTC-7) is already 2026-09-16 01:00 UTC — an
    // evening meeting that has crossed the UTC date boundary.
    const item: CalendarItemDto = {
      kind: 'Meeting',
      date: '2026-09-16T01:00:00.000Z',
      meeting: makeMeeting({ startsAtUtc: '2026-09-16T01:00:00.000Z' }),
    };
    expect(calendarItemLocalDateIso(item)).toBe('2026-09-15');
  });

  it('buckets an ordinary daytime meeting on the same local and UTC day', () => {
    const item: CalendarItemDto = {
      kind: 'Meeting',
      date: '2026-09-15T15:00:00.000Z',
      meeting: makeMeeting({ startsAtUtc: '2026-09-15T15:00:00.000Z' }),
    };
    expect(calendarItemLocalDateIso(item)).toBe('2026-09-15');
  });

  it('keeps an Obligation item as a plain UTC date slice — its date is a timezone-agnostic due date, not an instant', () => {
    const item: CalendarItemDto = { kind: 'Obligation', date: '2026-09-20T00:00:00.000Z' };
    expect(calendarItemLocalDateIso(item)).toBe('2026-09-20');
  });
});
