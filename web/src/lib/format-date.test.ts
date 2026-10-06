import { afterEach, describe, expect, it } from 'vitest';
import i18n from './i18n';
import { formatDate, toDateInputValue } from './format-date';

describe('formatDate', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('renders a date-only value as a short English date by default', () => {
    expect(formatDate('2026-09-15')).toBe('Sep 15, 2026');
  });

  it('renders the same date in Spanish once the active language is es', async () => {
    await i18n.changeLanguage('es');
    // Spanish month abbreviations are lowercase and unpunctuated ("sept"),
    // and day/month order flips relative to English — assert on content
    // rather than a brittle exact-string match across ICU versions.
    const text = formatDate('2026-09-15');
    expect(text).toContain('2026');
    expect(text.toLowerCase()).toContain('sept');
  });

  it('returns the fallback for empty/invalid input, regardless of language', () => {
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDate('not-a-date')).toBe('—');
    expect(formatDate('', 'n/a')).toBe('n/a');
  });

  it('parses a date-only value as a local calendar day (no timezone shift)', () => {
    // A birthday near midnight UTC must not shift to the previous/next day.
    expect(formatDate('2026-01-01')).toBe('Jan 1, 2026');
  });
});

describe('toDateInputValue', () => {
  it('slices the YYYY-MM-DD prefix', () => {
    expect(toDateInputValue('2026-09-15T18:00:00.000Z')).toBe('2026-09-15');
  });

  it('returns an empty string for empty input', () => {
    expect(toDateInputValue(null)).toBe('');
    expect(toDateInputValue(undefined)).toBe('');
  });
});
