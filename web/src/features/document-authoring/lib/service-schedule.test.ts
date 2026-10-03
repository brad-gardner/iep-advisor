import { describe, expect, it } from 'vitest';
import {
  formatDurationText,
  formatFrequencyText,
  formatScheduleSummary,
  parseDurationMinutes,
  parseFrequency,
  totalMinutesPerWeek,
} from './service-schedule';

describe('parseFrequency', () => {
  it.each([
    ['2 per week', { count: 2, period: 'week' }],
    ['2x/week', { count: 2, period: 'week' }],
    ['2 times weekly', { count: 2, period: 'week' }],
    ['twice a week', { count: 2, period: 'week' }],
    ['1x monthly', { count: 1, period: 'month' }],
    ['3 per month', { count: 3, period: 'month' }],
    ['daily', { count: 1, period: 'day' }],
    ['5x/day', { count: 5, period: 'day' }],
    ['once a week', { count: 1, period: 'week' }],
  ])('parses %s', (input, expected) => {
    expect(parseFrequency(input)).toEqual(expected);
  });

  it('returns null for text it does not recognize, keeping the free text intact', () => {
    expect(parseFrequency('as needed')).toBeNull();
    expect(parseFrequency('')).toBeNull();
    expect(parseFrequency(undefined)).toBeNull();
    expect(parseFrequency(null)).toBeNull();
  });

  it('round-trips through formatFrequencyText', () => {
    const parsed = parseFrequency('2x/week');
    expect(parsed).not.toBeNull();
    expect(formatFrequencyText(parsed!)).toBe('2 per week');
    expect(parseFrequency(formatFrequencyText(parsed!))).toEqual(parsed);
  });
});

describe('parseDurationMinutes', () => {
  it.each([
    ['30 minutes', 30],
    ['30 minute', 30],
    ['30 min', 30],
    ['30 mins', 30],
    ['30m', 30],
    ['30', 30],
  ])('parses %s', (input, expected) => {
    expect(parseDurationMinutes(input)).toBe(expected);
  });

  it('returns null for text it does not recognize', () => {
    expect(parseDurationMinutes('half an hour')).toBeNull();
    expect(parseDurationMinutes('')).toBeNull();
    expect(parseDurationMinutes(undefined)).toBeNull();
  });

  it('round-trips through formatDurationText, pluralizing correctly', () => {
    expect(formatDurationText(30)).toBe('30 minutes');
    expect(formatDurationText(1)).toBe('1 minute');
    expect(parseDurationMinutes(formatDurationText(30))).toBe(30);
  });
});

describe('totalMinutesPerWeek', () => {
  it('sums weekly rows directly', () => {
    const result = totalMinutesPerWeek([
      { frequencyText: '5 per week', durationText: '30 minutes' },
      { frequencyText: '1 per week', durationText: '25 minutes' },
    ]);
    expect(result).toEqual({ totalMinutesPerWeek: 175, includedCount: 2, excludedCount: 0 });
  });

  it('converts a monthly frequency to a weekly equivalent (÷4.33, rounded)', () => {
    const result = totalMinutesPerWeek([{ frequencyText: '2 per month', durationText: '60 minutes' }]);
    // 2 * 60 / 4.33 = 27.7... -> rounds to 28
    expect(result.totalMinutesPerWeek).toBe(28);
    expect(result.includedCount).toBe(1);
    expect(result.excludedCount).toBe(0);
  });

  it('converts a daily frequency to a weekly equivalent (×5)', () => {
    const result = totalMinutesPerWeek([{ frequencyText: '1 per day', durationText: '15 minutes' }]);
    expect(result.totalMinutesPerWeek).toBe(75);
  });

  it('excludes rows whose frequency or duration does not parse, and reports how many', () => {
    const result = totalMinutesPerWeek([
      { frequencyText: '5 per week', durationText: '30 minutes' },
      { frequencyText: 'as needed', durationText: '30 minutes' },
      { frequencyText: '2 per week', durationText: 'as long as it takes' },
      { frequencyText: undefined, durationText: undefined },
    ]);
    expect(result.totalMinutesPerWeek).toBe(150);
    expect(result.includedCount).toBe(1);
    expect(result.excludedCount).toBe(3);
  });

  it('returns a zero total for an empty schedule', () => {
    expect(totalMinutesPerWeek([])).toEqual({ totalMinutesPerWeek: 0, includedCount: 0, excludedCount: 0 });
  });
});

describe('formatScheduleSummary', () => {
  it('formats a parseable row as "N×/period · M min"', () => {
    expect(formatScheduleSummary('5 per week', '30 minutes')).toBe('5×/week · 30 min');
  });

  it('falls back to the raw text joined together when either side is unparseable', () => {
    expect(formatScheduleSummary('as needed', '30 minutes')).toBe('as needed · 30 minutes');
    expect(formatScheduleSummary('5 per week', undefined)).toBe('5 per week');
  });

  it('returns an empty string when both are blank', () => {
    expect(formatScheduleSummary(undefined, undefined)).toBe('');
    expect(formatScheduleSummary('', '')).toBe('');
  });
});
