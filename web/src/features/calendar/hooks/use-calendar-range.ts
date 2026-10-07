import { useState } from 'react';
import { getActiveLanguage } from '@/lib/i18n/format';

export interface CalendarDay {
  /** Local midnight for this cell. */
  date: Date;
  /** `YYYY-MM-DD` in local time — stable per-day key and query value. */
  iso: string;
  inCurrentMonth: boolean;
  isToday: boolean;
}

export interface UseCalendarRangeResult {
  /** e.g. "September 2026". */
  monthLabel: string;
  /** A 6-week (42-day) grid covering the visible month, Sunday-first. */
  days: CalendarDay[];
  /** `YYYY-MM-DD` bounds of the visible grid, for `GET /api/calendar/mine`. */
  rangeFromIso: string;
  rangeToIso: string;
  goToPreviousMonth: () => void;
  goToNextMonth: () => void;
  goToToday: () => void;
}

function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}

/** Month-grid state for the staff calendar: the visible 6-week grid, its
 * query range, and prev/next/today navigation. Pure date math — no fetching. */
export function useCalendarRange(initialDate: Date = new Date()): UseCalendarRangeResult {
  const [anchor, setAnchor] = useState(() => startOfMonth(initialDate));

  // Not memoized: cheap (42 date cells), and `monthLabel` must reflect the
  // active i18next language on every render — a `useMemo` keyed on `anchor`
  // alone would keep a stale-language `monthLabel` across a language switch
  // (react-i18next's `useTranslation` elsewhere is what triggers a host
  // component's re-render on switch; this hook just needs to not cache
  // around that). See `roster-columns.tsx`/`team-columns.tsx` for the same
  // "recompute every render" convention with translated builder output.
  const monthStart = startOfMonth(anchor);
  const gridStart = new Date(monthStart);
  gridStart.setDate(gridStart.getDate() - gridStart.getDay());

  const today = new Date();
  const days: CalendarDay[] = [];
  for (let i = 0; i < 42; i += 1) {
    const date = new Date(gridStart);
    date.setDate(gridStart.getDate() + i);
    days.push({
      date,
      iso: toIsoDate(date),
      inCurrentMonth: date.getMonth() === monthStart.getMonth(),
      isToday: isSameDay(date, today),
    });
  }

  const monthLabel = monthStart.toLocaleDateString(getActiveLanguage(), { month: 'long', year: 'numeric' });
  const rangeFromIso = days[0].iso;
  const rangeToIso = days[days.length - 1].iso;

  return {
    monthLabel,
    days,
    rangeFromIso,
    rangeToIso,
    goToPreviousMonth: () => setAnchor((prev) => new Date(prev.getFullYear(), prev.getMonth() - 1, 1)),
    goToNextMonth: () => setAnchor((prev) => new Date(prev.getFullYear(), prev.getMonth() + 1, 1)),
    goToToday: () => setAnchor(startOfMonth(new Date())),
  };
}
