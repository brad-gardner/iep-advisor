import { describe, it, expect } from 'vitest';
import {
  COMMON_TIME_ZONES,
  defaultMeetingTimeZone,
  formatMeetingWhen,
  timeZoneLabel,
  utcIsoToZonedParts,
  zonedDateTimeToUtcIso,
} from './meeting-time';

describe('zonedDateTimeToUtcIso', () => {
  it('converts a winter (EST, UTC-5) America/New_York time to UTC', () => {
    // 2026-01-15 14:00 EST => 19:00 UTC.
    const iso = zonedDateTimeToUtcIso('2026-01-15', '14:00', 'America/New_York');
    expect(iso).toBe('2026-01-15T19:00:00.000Z');
  });

  it('converts a summer (EDT, UTC-4) America/New_York time to UTC — proves DST is honored', () => {
    // 2026-07-15 14:00 EDT => 18:00 UTC (one hour earlier offset than winter).
    const iso = zonedDateTimeToUtcIso('2026-07-15', '14:00', 'America/New_York');
    expect(iso).toBe('2026-07-15T18:00:00.000Z');
  });

  it('handles a zone with no DST (America/Phoenix, always UTC-7)', () => {
    const winter = zonedDateTimeToUtcIso('2026-01-15', '09:00', 'America/Phoenix');
    const summer = zonedDateTimeToUtcIso('2026-07-15', '09:00', 'America/Phoenix');
    expect(winter).toBe('2026-01-15T16:00:00.000Z');
    expect(summer).toBe('2026-07-15T16:00:00.000Z');
  });
});

describe('utcIsoToZonedParts', () => {
  it('round-trips a UTC instant back to the original wall-clock date/time', () => {
    const iso = zonedDateTimeToUtcIso('2026-07-15', '14:00', 'America/New_York');
    expect(utcIsoToZonedParts(iso, 'America/New_York')).toEqual({ date: '2026-07-15', time: '14:00' });
  });

  it('round-trips across the winter offset too', () => {
    const iso = zonedDateTimeToUtcIso('2026-01-15', '09:30', 'America/New_York');
    expect(utcIsoToZonedParts(iso, 'America/New_York')).toEqual({ date: '2026-01-15', time: '09:30' });
  });
});

describe('formatMeetingWhen', () => {
  it('renders a start–end range and never throws on a valid instant', () => {
    const text = formatMeetingWhen('2026-07-15T18:00:00.000Z', 60);
    expect(text).toMatch(/2026/);
    expect(text).toContain('–');
  });

  it('falls back to an em dash for an invalid instant', () => {
    expect(formatMeetingWhen('not-a-date', 60)).toBe('—');
  });
});

describe('timeZoneLabel', () => {
  it('labels a known common zone', () => {
    expect(timeZoneLabel('America/New_York')).toContain('Eastern');
  });

  it('falls back to the raw id for an unlisted zone', () => {
    expect(timeZoneLabel('Europe/Paris')).toBe('Europe/Paris');
  });
});

describe('defaultMeetingTimeZone', () => {
  it('always returns one of the common zone ids', () => {
    const tz = defaultMeetingTimeZone();
    expect(COMMON_TIME_ZONES.some((z) => z.id === tz)).toBe(true);
  });
});
