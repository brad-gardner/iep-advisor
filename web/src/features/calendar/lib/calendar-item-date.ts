import { browserTimeZone, utcIsoToZonedParts } from '@/features/meetings/lib/meeting-time';
import type { CalendarItemDto } from '../types';

/**
 * The calendar day (`YYYY-MM-DD`) a calendar item belongs to, from the
 * *viewer's* perspective — used to bucket items into month-grid cells and the
 * agenda's "selected day" filter.
 *
 * `Meeting` items carry a UTC instant (`date` === `meeting.startsAtUtc`), so a
 * meeting scheduled in the evening in most US zones can already be the next
 * day in UTC; we convert to the browser's local wall-clock date instead of
 * slicing the raw UTC string. `Obligation` items carry a deliberately
 * timezone-agnostic UTC-midnight date-only value (a due date, not an
 * instant), so slicing the first 10 characters is correct and unambiguous for
 * them — converting it through a local time zone could shift it a day either
 * way depending on the viewer's offset.
 */
export function calendarItemLocalDateIso(item: CalendarItemDto): string {
  if (item.kind === 'Meeting') {
    return utcIsoToZonedParts(item.date, browserTimeZone()).date;
  }
  return item.date.slice(0, 10);
}
