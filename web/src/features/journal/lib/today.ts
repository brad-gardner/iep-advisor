/** Today's local calendar day as `yyyy-MM-dd` — the value an `<input type="date">` wants. */
export function todayInputValue(now: Date = new Date()): string {
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, '0');
  const d = String(now.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}
