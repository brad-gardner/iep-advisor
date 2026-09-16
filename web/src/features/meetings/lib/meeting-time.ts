/**
 * Time-zone helpers for scheduling meetings. Meetings are stored as a UTC
 * instant (`startsAtUtc`) plus the IANA zone the organizer scheduled in
 * (`timeZoneId`); the viewer always sees the wall-clock time rendered in
 * *their own browser's* time zone (`Intl` reads it from the environment
 * automatically once we hand it a `Date`), while any place that needs to say
 * "this meeting was scheduled in X" uses `timeZoneLabel` for that tz's own
 * label. No date library — everything here rides `Intl.DateTimeFormat`.
 */

export interface TimeZoneOption {
  id: string;
  label: string;
}

// A short, common list — not exhaustive IANA coverage. Covers US mainland +
// Alaska/Hawaii, which is the district's expected footprint for now.
export const COMMON_TIME_ZONES: TimeZoneOption[] = [
  { id: 'America/New_York', label: 'Eastern Time (America/New_York)' },
  { id: 'America/Chicago', label: 'Central Time (America/Chicago)' },
  { id: 'America/Denver', label: 'Mountain Time (America/Denver)' },
  { id: 'America/Phoenix', label: 'Mountain Time – no DST (America/Phoenix)' },
  { id: 'America/Los_Angeles', label: 'Pacific Time (America/Los_Angeles)' },
  { id: 'America/Anchorage', label: 'Alaska Time (America/Anchorage)' },
  { id: 'Pacific/Honolulu', label: 'Hawaii Time (Pacific/Honolulu)' },
];

export const DEFAULT_TIME_ZONE = 'America/New_York';

/** The viewer's browser time zone, falling back to the district default. */
export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_TIME_ZONE;
  } catch {
    return DEFAULT_TIME_ZONE;
  }
}

/** Default zone offered by the schedule form: the browser's zone when it's one
 * of the common options, else the district default. */
export function defaultMeetingTimeZone(): string {
  const tz = browserTimeZone();
  return COMMON_TIME_ZONES.some((z) => z.id === tz) ? tz : DEFAULT_TIME_ZONE;
}

/** A human label for a (possibly unlisted) IANA zone id. */
export function timeZoneLabel(timeZoneId: string): string {
  return COMMON_TIME_ZONES.find((z) => z.id === timeZoneId)?.label ?? timeZoneId;
}

// Minutes to ADD to a wall-clock time in `timeZone` to get UTC, evaluated at
// `instant` (an approximate UTC instant near the wall-clock time — offsets
// vary across DST, so the caller re-derives from a first guess).
function utcOffsetMinutesAt(instant: Date, timeZone: string): number {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(instant);
  const map: Record<string, string> = {};
  for (const part of parts) map[part.type] = part.value;
  const asUtc = Date.UTC(
    Number(map.year),
    Number(map.month) - 1,
    Number(map.day),
    Number(map.hour),
    Number(map.minute),
    Number(map.second)
  );
  return (asUtc - instant.getTime()) / 60000;
}

/**
 * Convert a wall-clock `date` (`YYYY-MM-DD`) + `time` (`HH:mm`) in `timeZone`
 * to a UTC ISO instant. Self-correcting across DST: a single guess (treating
 * the wall clock as UTC, then reading the zone's real offset at that guessed
 * instant) can land on the wrong side of a transition for wall-clock times in
 * the hour or two right after it — the guessed *instant* is in the old
 * regime even though the wall-clock *value* the caller means is in the new
 * one, or vice versa. So we re-derive the offset at the first candidate and
 * refine:
 *  - If the re-derived offset agrees with the first guess, we've converged —
 *    this also correctly resolves an ambiguous fall-back wall-clock time
 *    (one that occurs twice) to its *earlier* occurrence, since the initial
 *    guess always lands before the transition for the early-morning wall
 *    times where these transitions happen.
 *  - If it disagrees, recompute a second candidate from the new offset. If
 *    *that* candidate's own offset is self-consistent, it's the unique
 *    correct answer (a wall-clock time near, but not on, a transition).
 *  - If neither candidate is self-consistent, the wall-clock time falls in a
 *    spring-forward gap and never existed; we resolve it by shifting forward
 *    to the later candidate, landing on the first real instant after the
 *    gap (consistent with how calendar UIs generally treat a "missing" time).
 */
export function zonedDateTimeToUtcIso(date: string, time: string, timeZone: string): string {
  const [year, month, day] = date.split('-').map(Number);
  const [hour, minute] = time.split(':').map(Number);
  const wallUtcMs = Date.UTC(year, (month || 1) - 1, day || 1, hour || 0, minute || 0, 0);

  const offsetA = utcOffsetMinutesAt(new Date(wallUtcMs), timeZone);
  const candidateA = wallUtcMs - offsetA * 60000;

  const offsetB = utcOffsetMinutesAt(new Date(candidateA), timeZone);
  if (offsetB === offsetA) {
    return new Date(candidateA).toISOString();
  }

  const candidateB = wallUtcMs - offsetB * 60000;
  const offsetAtCandidateB = utcOffsetMinutesAt(new Date(candidateB), timeZone);
  if (offsetAtCandidateB === offsetB) {
    return new Date(candidateB).toISOString();
  }

  // Neither candidate is self-consistent: a spring-forward gap. Shift
  // forward rather than back.
  return new Date(Math.max(candidateA, candidateB)).toISOString();
}

/** Split a UTC ISO instant into wall-clock `{ date, time }` strings for
 * `timeZone` — used to pre-fill the schedule form when rescheduling. */
export function utcIsoToZonedParts(iso: string, timeZone: string): { date: string; time: string } {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hourCycle: 'h23',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).formatToParts(new Date(iso));
  const map: Record<string, string> = {};
  for (const part of parts) map[part.type] = part.value;
  return { date: `${map.year}-${map.month}-${map.day}`, time: `${map.hour}:${map.minute}` };
}

/**
 * Render a meeting's start–end window in the *viewer's browser* time zone
 * (via `Intl`'s environment default), e.g. "Sep 20, 2026, 3:00 – 4:00 PM".
 * Returns `'—'` for an unparseable instant.
 */
export function formatMeetingWhen(startsAtUtc: string, durationMinutes: number): string {
  const start = new Date(startsAtUtc);
  if (Number.isNaN(start.getTime())) return '—';
  const end = new Date(start.getTime() + durationMinutes * 60000);
  const dateFmt = new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
  const timeFmt = new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' });
  return `${dateFmt.format(start)}, ${timeFmt.format(start)} – ${timeFmt.format(end)}`;
}
