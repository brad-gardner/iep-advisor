import { useCallback, useEffect, useRef, useState } from 'react';
import { MoreVertical } from 'lucide-react';
import { cn } from '@/lib/cn';

export interface MenuItem {
  label: string;
  onSelect: () => void;
  icon?: React.ReactNode;
  /** `danger` tints the item for destructive actions (delete/revoke). */
  variant?: 'default' | 'danger';
  disabled?: boolean;
  'data-testid'?: string;
}

interface MenuProps {
  /** Accessible name for the trigger, e.g. "Actions for Lincoln Elementary". */
  label: string;
  items: MenuItem[];
  /** Trigger content; defaults to a kebab (⋮) icon. */
  trigger?: React.ReactNode;
  /** Alignment of the popover relative to the trigger. */
  align?: 'left' | 'right';
  'data-testid'?: string;
}

/**
 * Menu-button (WAI-ARIA APG). The trigger exposes `aria-haspopup="menu"` +
 * `aria-expanded`; the popover is a `role="menu"` of `role="menuitem"`s with
 * arrow / Home / End navigation, Esc-to-close with return-focus, and
 * click-outside dismissal. Replaces the app's hand-rolled dropdowns and backs
 * the Table row kebab.
 */
export function Menu({
  label,
  items,
  trigger,
  align = 'right',
  'data-testid': testId,
}: MenuProps) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const itemRefs = useRef<Array<HTMLButtonElement | null>>([]);

  const close = (returnFocus = true) => {
    setOpen(false);
    if (returnFocus) triggerRef.current?.focus();
  };

  // Focus the first enabled item at/after `start`, searching in `direction` and
  // wrapping. Disabled items are skipped (APG: roving focus never lands on a
  // disabled menuitem — `.focus()` on a disabled button is a no-op that would
  // otherwise strand the roving model).
  const focusItemAt = useCallback(
    (start: number, direction: 1 | -1) => {
      const count = items.length;
      for (let i = 0; i < count; i += 1) {
        const idx = (((start + i * direction) % count) + count) % count;
        if (!items[idx]?.disabled) {
          itemRefs.current[idx]?.focus();
          return;
        }
      }
    },
    [items],
  );

  // Move focus to the first enabled item when the menu opens (APG: menu-button
  // opened by click/Enter focuses the first item).
  useEffect(() => {
    if (open) focusItemAt(0, 1);
  }, [open, focusItemAt]);

  // Dismiss on any pointer press outside the menu (no focus return — the user
  // is interacting elsewhere).
  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [open]);

  const currentIndex = () =>
    itemRefs.current.findIndex((el) => el === document.activeElement);

  const handleMenuKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        focusItemAt(currentIndex() + 1, 1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        focusItemAt(currentIndex() - 1, -1);
        break;
      case 'Home':
        event.preventDefault();
        focusItemAt(0, 1);
        break;
      case 'End':
        event.preventDefault();
        focusItemAt(items.length - 1, -1);
        break;
      case 'Escape':
        event.preventDefault();
        close();
        break;
      case 'Tab':
        // Tabbing out closes without stealing focus back.
        setOpen(false);
        break;
    }
  };

  const handleTriggerKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      setOpen(true);
    }
  };

  const select = (item: MenuItem) => {
    if (item.disabled) return;
    close();
    item.onSelect();
  };

  return (
    <div ref={containerRef} className="relative inline-block text-left">
      <button
        ref={triggerRef}
        type="button"
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        data-testid={testId}
        onClick={() => setOpen((v) => !v)}
        onKeyDown={handleTriggerKeyDown}
        className="flex h-9 w-9 items-center justify-center rounded-button text-brand-slate-400 transition-colors hover:bg-brand-slate-50 hover:text-brand-slate-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
      >
        {trigger ?? <MoreVertical className="h-4 w-4" strokeWidth={1.8} aria-hidden="true" />}
      </button>

      {open && (
        <div
          role="menu"
          aria-label={label}
          onKeyDown={handleMenuKeyDown}
          className={cn(
            'absolute z-20 mt-1 min-w-44 overflow-hidden rounded-card border border-brand-slate-200 bg-white py-1 shadow-lg',
            align === 'right' ? 'right-0' : 'left-0',
            'motion-safe:animate-overlay-in',
          )}
        >
          {items.map((item, index) => (
            <button
              key={item.label}
              ref={(el) => {
                itemRefs.current[index] = el;
              }}
              type="button"
              role="menuitem"
              tabIndex={-1}
              disabled={item.disabled}
              data-testid={item['data-testid']}
              onClick={() => select(item)}
              className={cn(
                'flex w-full items-center gap-2 px-3 py-2 text-left text-[13px] transition-colors focus:outline-none disabled:cursor-not-allowed disabled:opacity-50',
                item.variant === 'danger'
                  ? 'text-brand-danger-600 hover:bg-brand-danger-50 focus-visible:bg-brand-danger-50'
                  : 'text-brand-slate-700 hover:bg-brand-slate-50 focus-visible:bg-brand-slate-50',
              )}
            >
              {item.icon && <span className="shrink-0" aria-hidden="true">{item.icon}</span>}
              {item.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
