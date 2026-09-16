import { useRef } from 'react';
import { cn } from '@/lib/cn';
import type { CalendarItemDto } from '../types';
import type { CalendarDay } from '../hooks/use-calendar-range';

const WEEKDAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

interface CalendarMonthGridProps {
  days: CalendarDay[];
  items: CalendarItemDto[];
  selectedIso: string | null;
  onSelectDay: (iso: string) => void;
}

function itemDateIso(item: CalendarItemDto): string {
  return item.date.slice(0, 10);
}

/**
 * A 7-column, 6-week month grid. Each day is a `<button>` in one roving
 * tabindex group (APG grid pattern, simplified): only the active/selected day
 * is tab-stoppable, and arrow keys move focus (and selection) by day, Home/End
 * by week, so keyboard users can scan the whole month without tabbing through
 * 42 cells individually.
 */
export function CalendarMonthGrid({ days, items, selectedIso, onSelectDay }: CalendarMonthGridProps) {
  const buttonRefs = useRef<Array<HTMLButtonElement | null>>([]);

  const countFor = (iso: string) => items.filter((item) => itemDateIso(item) === iso).length;

  const activeIndex = Math.max(
    0,
    days.findIndex((d) => d.iso === (selectedIso ?? days.find((x) => x.isToday)?.iso))
  );

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
    <div role="grid" aria-label="Calendar month" className="rounded-card border border-brand-slate-200 overflow-hidden">
      <div role="row" className="grid grid-cols-7 border-b border-brand-slate-200 bg-brand-slate-50">
        {WEEKDAY_LABELS.map((label) => (
          <div key={label} role="columnheader" className="px-2 py-2 text-center text-xs font-medium text-brand-slate-500">
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
                <button
                  key={day.iso}
                  ref={(el) => {
                    buttonRefs.current[index] = el;
                  }}
                  type="button"
                  role="gridcell"
                  tabIndex={index === activeIndex ? 0 : -1}
                  aria-current={day.isToday ? 'date' : undefined}
                  aria-selected={isSelected}
                  data-testid={`calendar-day-${day.iso}`}
                  onClick={() => onSelectDay(day.iso)}
                  onKeyDown={(e) => handleKeyDown(e, index)}
                  className={cn(
                    'flex h-16 flex-col items-center justify-start gap-1 p-1.5 text-sm transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-brand-teal-400',
                    day.inCurrentMonth ? 'text-brand-slate-700' : 'text-brand-slate-300',
                    isSelected && 'bg-brand-teal-50',
                    !isSelected && 'hover:bg-brand-slate-50'
                  )}
                >
                  <span
                    className={cn(
                      'flex h-6 w-6 items-center justify-center rounded-full text-xs',
                      day.isToday && 'bg-brand-teal-500 font-semibold text-white'
                    )}
                  >
                    {day.date.getDate()}
                  </span>
                  {count > 0 && (
                    <span className="rounded-full bg-brand-slate-200 px-1.5 text-[10px] font-medium text-brand-slate-600">
                      {count}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
