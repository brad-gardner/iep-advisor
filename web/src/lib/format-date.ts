/**
 * Render an ISO date (`YYYY-MM-DD`) or timestamp as a short local date, e.g.
 * "Sep 15, 2026". Date-only values are parsed as local calendar days so a
 * birthday never shifts by a day across timezones. Returns `fallback` for
 * empty/invalid input.
 */
export function formatDate(iso: string | null | undefined, fallback = '—'): string {
  if (!iso) return fallback;
  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  const date = dateOnly
    ? new Date(Number(dateOnly[1]), Number(dateOnly[2]) - 1, Number(dateOnly[3]))
    : new Date(iso);
  if (Number.isNaN(date.getTime())) return fallback;
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

/** The `YYYY-MM-DD` slice of an ISO value, for `<input type="date">`. */
export function toDateInputValue(iso: string | null | undefined): string {
  if (!iso) return '';
  return iso.slice(0, 10);
}
