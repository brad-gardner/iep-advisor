/**
 * Parse/format helpers for the services schedule (plan 2026-10-02-002, Phase
 * 4). Frequency and duration stay stored as plain text in the template's
 * existing `frequency` / `duration` columns — no new reserved row keys — so
 * the PDF, AI and completeness rules are untouched. These helpers only
 * translate between that text and the structured {count, period} / minutes
 * shapes the inline editor's controls use, and compute the read view's
 * minutes/week total.
 *
 * Parsing is intentionally forgiving (a handful of common phrasings) and
 * returns `null` for anything else, so an editor can always fall back to a
 * free-text input that preserves whatever a district already typed instead of
 * destroying it.
 */

export type ServicePeriod = 'day' | 'week' | 'month';

export interface ParsedFrequency {
  count: number;
  period: ServicePeriod;
}

const WORD_NUMBERS: Record<string, number> = {
  once: 1,
  twice: 2,
  thrice: 3,
  one: 1,
  two: 2,
  three: 3,
  four: 4,
  five: 5,
  six: 6,
  seven: 7,
  eight: 8,
  nine: 9,
  ten: 10,
};

const PERIOD_PATTERNS: Array<{ re: RegExp; period: ServicePeriod }> = [
  { re: /week/i, period: 'week' },
  { re: /month/i, period: 'month' },
  // "day"/"days" as a whole word, or "daily" — NOT a substring match (unlike
  // week/month above): "daily" doesn't literally contain "day" (d-a-i-l-y).
  { re: /daily|\bdays?\b/i, period: 'day' },
];

/**
 * Parses common frequency phrasings — "2 per week", "2x/week", "2 times
 * weekly", "twice a week", "1x monthly", "daily" — into a count + period.
 * Returns `null` for anything it doesn't recognize (the caller keeps the raw
 * text in a free-text fallback rather than discarding it).
 */
export function parseFrequency(raw: string | null | undefined): ParsedFrequency | null {
  if (typeof raw !== 'string') return null;
  const text = raw.trim().toLowerCase();
  if (!text) return null;

  const period = PERIOD_PATTERNS.find((p) => p.re.test(text))?.period;
  if (!period) return null;

  let count: number | undefined;
  const digitMatch = text.match(/(\d+)/);
  if (digitMatch) {
    count = Number(digitMatch[1]);
  } else {
    for (const [word, n] of Object.entries(WORD_NUMBERS)) {
      if (new RegExp(`\\b${word}\\b`).test(text)) {
        count = n;
        break;
      }
    }
  }
  if (count == null && /^(weekly|monthly|daily)$/.test(text)) count = 1;
  if (count == null || !Number.isFinite(count) || count <= 0) return null;

  return { count, period };
}

/** Normalized frequency text: "2 per week", "1 per month". */
export function formatFrequencyText(f: ParsedFrequency): string {
  return `${f.count} per ${f.period}`;
}

/**
 * Parses common duration phrasings — "30 minutes", "30 min", "30 mins",
 * "30m", or a bare number — into whole minutes. Returns `null` for anything
 * else.
 */
export function parseDurationMinutes(raw: string | null | undefined): number | null {
  if (typeof raw !== 'string') return null;
  const text = raw.trim().toLowerCase();
  if (!text) return null;
  const match = text.match(/^(\d+(?:\.\d+)?)\s*(minutes?|mins?|m)?$/);
  if (!match) return null;
  const n = Number(match[1]);
  if (!Number.isFinite(n) || n <= 0) return null;
  return Math.round(n);
}

/** Normalized duration text: "30 minutes" (singular "1 minute"). */
export function formatDurationText(minutes: number): string {
  return `${minutes} minute${minutes === 1 ? '' : 's'}`;
}

/** Minutes/week a single row contributes, given its parsed frequency and
 *  minutes-per-session. Monthly frequencies divide by the average weeks per
 *  month (4.33) and round; daily frequencies multiply by a 5-day school week. */
export function minutesPerWeekForRow(count: number, period: ServicePeriod, minutesPerSession: number): number {
  switch (period) {
    case 'week':
      return count * minutesPerSession;
    case 'month':
      return Math.round((count * minutesPerSession) / 4.33);
    case 'day':
      return count * minutesPerSession * 5;
    default:
      return 0;
  }
}

export interface ServiceScheduleRow {
  frequencyText: string | null | undefined;
  durationText: string | null | undefined;
}

export interface ServiceScheduleTotal {
  totalMinutesPerWeek: number;
  /** Rows whose frequency AND duration both parsed and were counted. */
  includedCount: number;
  /** Rows excluded from the total because frequency and/or duration didn't parse. */
  excludedCount: number;
}

/** Sums minutes/week over rows whose frequency and duration both parse,
 *  reporting how many rows were excluded so the read view can note it. */
export function totalMinutesPerWeek(rows: ServiceScheduleRow[]): ServiceScheduleTotal {
  let total = 0;
  let included = 0;
  let excluded = 0;
  for (const row of rows) {
    const freq = parseFrequency(row.frequencyText);
    const minutes = parseDurationMinutes(row.durationText);
    if (freq && minutes != null) {
      total += minutesPerWeekForRow(freq.count, freq.period, minutes);
      included += 1;
    } else {
      excluded += 1;
    }
  }
  return { totalMinutesPerWeek: total, includedCount: included, excludedCount: excluded };
}

/**
 * Best-effort one-line schedule summary for a read-mode row — "5×/week · 30
 * min" when both parse, else whatever raw text exists joined together, else
 * empty (the caller shows its own "Not set"-style fallback).
 */
export function formatScheduleSummary(
  frequencyText: string | null | undefined,
  durationText: string | null | undefined
): string {
  const freq = typeof frequencyText === 'string' ? frequencyText.trim() : '';
  const dur = typeof durationText === 'string' ? durationText.trim() : '';
  const parsedFreq = parseFrequency(freq);
  const minutes = parseDurationMinutes(dur);
  if (parsedFreq && minutes != null) {
    return `${parsedFreq.count}×/${parsedFreq.period} · ${minutes} min`;
  }
  return [freq, dur].filter(Boolean).join(' · ');
}
