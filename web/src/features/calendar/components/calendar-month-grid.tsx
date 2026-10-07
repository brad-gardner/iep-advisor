import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/lib/cn';
import { getActiveLanguage } from '@/lib/i18n/format';
import { calendarItemLocalDateIso } from '../lib/calendar-item-date';
import type { CalendarItemDto } from '../types';
import type { CalendarDay } from '../hooks/use-calendar-range';

// Computed from the active i18next language (`Intl.DateTimeFormat`, keyed on
// a reference Sunday) rather than a static English array, so the column
// headers — and the day names Intl weaves into `dayAriaLabel` below — follow
// the active language like every other date-facing text in the app (see
// `docs/i18n/README.md`'s "Formatting"). `new Date(2024, 0, 7)` is simply a
// known Sunday; the grid it labels is unrelated to that year.
function weekdayLabels(language: string): string[] {
  const formatter = new Intl.DateTimeFormat(language, { weekday: 'short' });
  const referenceSunday = new Date(2024, 0, 7);
  return Array.from({ length: 7 }, (_, i) => {
    const date = new Date(referenceSunday);
    date.setDate(date.getDate() + i);
    return formatter.format(date);
  });
}

interface CalendarMonthGridProps {
  days: CalendarDay[];
  items: CalendarItemDto[];
  selectedIso: string | null;
  onSelectDay: (iso: string) => void;
}

/**
 * A 7-column, 6-week month grid. Each day is a `<button>` in one roving
 * tabindex group (APG grid pattern, simplified): only the active/selected day
 * is tab-stoppable, and arrow keys move focus (and selection) by day, Home/End
 * by week, so keyboard users can scan the whole month without tabbing through
 * 42 cells individually. Each button sits inside a plain `role="gridcell"`
 * wrapper (not on the button itself) so the button keeps its native,
 * screen-reader-announced button semantics — APG's own grid pattern warns
 * that overriding a button's role to `gridcell` silences that announcement.
 */
export function CalendarMonthGrid({ days, items, selectedIso, onSelectDay }: CalendarMonthGridProps) {
  const { t } = useTranslation('calendar');
  const language = getActiveLanguage();
  const buttonRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const labels = weekdayLabels(language);

  const countFor = (iso: string) => items.filter((item) => calendarItemLocalDateIso(item) === iso).length;

  function dayAriaLabel(day: CalendarDay, count: number): string {
    const dateLabel = day.date.toLocaleDateString(language, {
      weekday: 'long',
      month: 'long',
      day: 'numeric',
      year: 'numeric',
    });
    const parts = [dateLabel];
    if (day.isToday) parts.push(t('monthGrid.today'));
    if (count > 0) parts.push(t('monthGrid.itemCount', { count }));
    return parts.join(', ');
  }

  // Prefer the selected day's index; if it isn't part of the currently
  // visible grid at all (e.g. a stale selection left over from a month the
  // user has since navigated away from), fall back to today's cell rather
  // than always landing on the grid's first (often dimmed, previous-month)
  // cell.
  const selectedIndex = selectedIso ? days.findIndex((d) => d.iso === selectedIso) : -1;
  const todayIndex = days.findIndex((d) => d.isToday);
  const activeIndex = selectedIndex >= 0 ? selectedIndex : Math.max(0, todayIndex);

  const focusAndSelect = (index: number) => {
    const clamped = Math.max(0, Math.min(days.length - 1, index));
    buttonRefs.current[clamped]?.focus();
    onSelectDay(days[clamped].iso);
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    switch (event.key) {
      case 'ArrowRight':
        event.preventDefault();
        focusAndSelect(index + 1);
        break;
      case 'ArrowLeft':
        event.preventDefault();
        focusAndSelect(index - 1);
        break;
      case 'ArrowDown':
        event.preventDefault();
        focusAndSelect(index + 7);
        break;
      case 'ArrowUp':
        event.preventDefault();
        focusAndSelect(index - 7);
        break;
      case 'Home':
        event.preventDefault();
        focusAndSelect(index - (index % 7));
        break;
      case 'End':
        event.preventDefault();
        focusAndSelect(index - (index % 7) + 6);
        break;
    }
  };

  return (
    <div
      role="grid"
      aria-label={t('monthGrid.ariaLabel')}
      className="rounded-card border border-brand-slate-200 overflow-hidden"
    >
      <div role="row" className="grid grid-cols-7 border-b border-brand-slate-200 bg-brand-slate-50">
        {labels.map((label, i) => (
          <div key={i} role="columnheader" className="px-2 py-2 text-center text-xs font-medium text-brand-slate-500">
            {label}
          </div>
        ))}
      </div>
      <div role="rowgroup">
        {Array.from({ length: 6 }).map((_, week) => (
          <div key={week} role="row" className="grid grid-cols-7 divide-x divide-brand-slate-100 border-b border-brand-slate-100 last:border-b-0">
            {days.slice(week * 7, week * 7 + 7).map((day, colIndex) => {
              const index = week * 7 + colIndex;
              const count = countFor(day.iso);
              const isSelected = selectedIso === day.iso;
              return (
                <div key={day.iso} role="gridcell" aria-selected={isSelected}>
                  <button
                    ref={(el) => {
                      buttonRefs.current[index] = el;
                    }}
                    type="button"
                    tabIndex={index === activeIndex ? 0 : -1}
                    aria-current={day.isToday ? 'date' : undefined}
                    aria-label={dayAriaLabel(day, count)}
                    data-testid={`calendar-day-${day.iso}`}
                    onClick={() => onSelectDay(day.iso)}
                    onKeyDown={(e) => handleKeyDown(e, index)}
                    className={cn(
                      'flex h-16 w-full flex-col items-center justify-start gap-1 p-1.5 text-sm transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-brand-teal-500',
                      day.inCurrentMonth ? 'text-brand-slate-700' : 'text-brand-slate-500',
                      isSelected && 'bg-brand-teal-50',
                      !isSelected && 'hover:bg-brand-slate-50'
                    )}
                  >
                    <span
                      aria-hidden="true"
                      className={cn(
                        'flex h-6 w-6 items-center justify-center rounded-full text-xs',
                        day.isToday && 'bg-brand-teal-500 font-semibold text-white'
                      )}
                    >
                      {day.date.getDate()}
                    </span>
                    {count > 0 && (
                      <span
                        aria-hidden="true"
                        className="rounded-full bg-brand-slate-200 px-1.5 text-[10px] font-medium text-brand-slate-600"
                      >
                        {count}
                      </span>
                    )}
                  </button>
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
