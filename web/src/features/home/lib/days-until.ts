// Calendar-day difference between `startsAtUtc` and "now" — recomputed live
// rather than trusted as a one-time server snapshot, so a tab left open
// across a day boundary (or past the meeting) never keeps showing a stale
// "Tomorrow"/"In 3 days" badge. Exported for the same computation to be unit
// tested without mounting the card.
export function daysUntilFromNow(startsAtUtc: string, now: Date = new Date()): number {
  const start = new Date(startsAtUtc);
  const startDay = Date.UTC(start.getFullYear(), start.getMonth(), start.getDate());
  const nowDay = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((startDay - nowDay) / 86_400_000);
}
