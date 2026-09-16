export const COMPLIANCE_RANGE_PRESETS = [30, 60, 90] as const;
export type ComplianceRangeDays = (typeof COMPLIANCE_RANGE_PRESETS)[number];

function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** `{ from, to }` for the compliance board's "due" buckets: today through
 * today + `days`. Overdue buckets ignore this range (always due < today). */
export function complianceDateRange(
  days: ComplianceRangeDays,
  today: Date = new Date()
): { from: string; to: string } {
  const to = new Date(today);
  to.setDate(to.getDate() + days);
  return { from: toIsoDate(today), to: toIsoDate(to) };
}
