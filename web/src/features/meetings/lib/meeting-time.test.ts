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

  describe('near the America/New_York spring-forward transition (2026-03-08, 02:00 -> 03:00)', () => {
    it('round-trips ordinary hours on both sides of the transition without a one-hour drift', () => {
      for (const time of ['00:00', '00:30', '01:00', '01:30', '04:00', '05:30', '07:00']) {
        const iso = zonedDateTimeToUtcIso('2026-03-08', time, 'America/New_York');
        expect(utcIsoToZonedParts(iso, 'America/New_York')).toEqual({ date: '2026-03-08', time });
      }
    });

    it('shifts a non-existent 02:xx wall-clock time forward into the real 03:xx it becomes', () => {
      const at0200 = zonedDateTimeToUtcIso('2026-03-08', '02:00', 'America/New_York');
      const at0230 = zonedDateTimeToUtcIso('2026-03-08', '02:30', 'America/New_York');
      expect(at0200).toBe('2026-03-08T07:00:00.000Z');
      expect(at0230).toBe('2026-03-08T07:30:00.000Z');
      // Both land on the real, post-gap wall-clock time exactly one hour later.
      expect(utcIsoToZonedParts(at0200, 'America/New_York')).toEqual({ date: '2026-03-08', time: '03:00' });
      expect(utcIsoToZonedParts(at0230, 'America/New_York')).toEqual({ date: '2026-03-08', time: '03:30' });
    });
  });

  describe('near the America/New_York fall-back transition (2026-11-01, 02:00 -> 01:00)', () => {
    it('round-trips every wall-clock hour from 00:00 through 07:00, including the repeated 01:xx hour', () => {
      for (const time of ['00:00', '00:30', '01:00', '01:30', '02:00', '02:30', '04:00', '07:00']) {
        const iso = zonedDateTimeToUtcIso('2026-11-01', time, 'America/New_York');
        expect(utcIsoToZonedParts(iso, 'America/New_York')).toEqual({ date: '2026-11-01', time });
      }
    });

    it('resolves the ambiguous 01:xx hour to its earlier (EDT) occurrence', () => {
      const iso = zonedDateTimeToUtcIso('2026-11-01', '01:30', 'America/New_York');
      // 01:30 EDT (the earlier occurrence) is 05:30 UTC; 01:30 EST (the later,
      // second occurrence of the same wall-clock reading) would be 06:30 UTC.
      expect(iso).toBe('2026-11-01T05:30:00.000Z');
    });
  });

  describe("near Europe/Berlin's own spring-forward transition (2026-03-29, 02:00 -> 03:00)", () => {
    it('round-trips ordinary hours and shifts the non-existent 02:xx hour forward', () => {
      for (const time of ['00:30', '01:30', '04:00', '06:30']) {
        const iso = zonedDateTimeToUtcIso('2026-03-29', time, 'Europe/Berlin');
        expect(utcIsoToZonedParts(iso, 'Europe/Berlin')).toEqual({ date: '2026-03-29', time });
      }

      const at0200 = zonedDateTimeToUtcIso('2026-03-29', '02:00', 'Europe/Berlin');
      expect(utcIsoToZonedParts(at0200, 'Europe/Berlin')).toEqual({ date: '2026-03-29', time: '03:00' });
    });
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
