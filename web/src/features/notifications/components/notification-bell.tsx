import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { cn } from '@/lib/cn';
import { formatDate } from '@/lib/format-date';
import { listNotifications, markNotificationRead } from '../api/notifications-api';
import { useNotificationsContext } from '../hooks/use-notifications-context';
import type { NotificationDto } from '../types';

/**
 * Sidebar bell: unread badge (shared, see `NotificationsProvider`) + a
 * dropdown of the latest 5 with a "See all" link to `/notifications`. The
 * dropdown is portaled to `<body>` so it is never clipped by the sidebar's
 * layout, and its keyboard behavior — Escape closes and returns focus to the
 * trigger, Arrow/Home/End rove focus between items, the first item is
 * focused when the menu opens — mirrors `components/ui/menu.tsx`'s `Menu`
 * primitive, which this can't compose directly (its items need a two-line
 * title/timestamp template and an unread tint, not `Menu`'s single-line
 * item shape).
 */
export function NotificationBell() {
  const { unreadCount, refresh } = useNotificationsContext();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<NotificationDto[] | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  // The dropdown is portaled to `<body>`, so it is NOT a DOM descendant of
  // `containerRef` — the outside-click check below must also exempt it, or a
  // pointerdown on any dropdown item (including its own real `<a>` targets)
  // closes the menu before the click ever reaches React's handler.
  const menuRef = useRef<HTMLDivElement>(null);
  // One ref per focusable menu row: the notification links (when loaded and
  // non-empty), then always the trailing "See all" link.
  const itemRefs = useRef<Array<HTMLAnchorElement | null>>([]);
  const [coords, setCoords] = useState<{ top: number; left: number } | null>(null);

  useEffect(() => {
    if (!open) return;
    let active = true;
    (async () => {
      try {
        const response = await listNotifications({ limit: 5 });
        if (active && response.success && response.data) setItems(response.data.items);
      } catch {
        if (active) setItems([]);
      }
    })();
    return () => {
      active = false;
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!containerRef.current?.contains(target) && !menuRef.current?.contains(target)) {
        setOpen(false);
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [open]);

  // Focus the first focusable row (APG: opening a menu-button focuses its
  // first item) once there's something real to focus — while `items` is
  // still `null` the dropdown only shows a "Loading…" placeholder. Focus is
  // moved once per open: a later refetch replacing `items` while the menu is
  // already open must not yank focus away from wherever the user arrowed to.
  const focusedForOpenRef = useRef(false);
  useEffect(() => {
    if (!open) {
      focusedForOpenRef.current = false;
      return;
    }
    if (items !== null && !focusedForOpenRef.current) {
      focusedForOpenRef.current = true;
      itemRefs.current[0]?.focus();
    }
  }, [open, items]);

  const focusItemAt = useCallback((start: number, direction: 1 | -1) => {
    const count = itemRefs.current.length;
    if (count === 0) return;
    for (let i = 0; i < count; i += 1) {
      const idx = (((start + i * direction) % count) + count) % count;
      if (itemRefs.current[idx]) {
        itemRefs.current[idx]?.focus();
        return;
      }
    }
  }, []);

  const currentIndex = () => itemRefs.current.findIndex((el) => el === document.activeElement);

  const close = (returnFocus = true) => {
    setOpen(false);
    if (returnFocus) triggerRef.current?.focus();
  };

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
        focusItemAt(itemRefs.current.length - 1, -1);
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

  const openMenu = () => {
    const rect = triggerRef.current?.getBoundingClientRect();
    if (rect) setCoords({ top: rect.bottom + 4, left: rect.left });
    setOpen(true);
  };

  const handleItemClick = (notification: NotificationDto) => {
    // Deferred: this handler runs before React Router's own click handler on
    // the same `<Link>` (which calls `preventDefault()` and navigates via
    // history). Closing synchronously would unmount the link mid-click and
    // let the browser fall through to a real anchor navigation instead.
    setTimeout(() => setOpen(false), 0);
    if (notification.readAt) return;
    // Best-effort: the dropdown is closing regardless (the user is
    // navigating away via this same click), and the next poll or a visit to
    // `/notifications` will show the true read state either way, so a
    // transient failure here isn't worth a toast on top of the navigation.
    markNotificationRead(notification.id)
      .then(refresh)
      .catch(() => undefined);
  };

  // Each row's own ref callback (below) keeps `itemRefs.current` in sync as
  // `items` changes — including the one-time transition from "loading" (only
  // "See all" is focusable, at index 0) to a loaded, non-empty list
  // (notification rows at 0..n-1, "See all" at n) — so there's nothing to
  // reset here in the render body itself.
  const notificationItemCount = items && items.length > 0 ? items.length : 0;

  return (
    <div ref={containerRef} className="relative">
      <button
        ref={triggerRef}
        type="button"
        aria-label={unreadCount ? `Notifications, ${unreadCount} unread` : 'Notifications'}
        aria-haspopup="menu"
        aria-expanded={open}
        data-testid="notification-bell"
        onClick={() => (open ? setOpen(false) : openMenu())}
        className="relative flex h-9 w-9 items-center justify-center rounded-button text-brand-slate-400 transition-colors hover:bg-brand-slate-700 hover:text-brand-slate-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-400"
      >
        <Bell size={18} strokeWidth={1.8} aria-hidden="true" />
        {!!unreadCount && (
          <span
            className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-brand-danger-500 px-1 text-[10px] font-semibold text-white"
            aria-hidden="true"
            data-testid="notification-bell-badge"
          >
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>
      <span className="sr-only" role="status" aria-live="polite" data-testid="notification-unread-live">
        {unreadCount ? `${unreadCount} unread notification${unreadCount === 1 ? '' : 's'}` : ''}
      </span>

      {open &&
        createPortal(
          <div
            ref={menuRef}
            role="menu"
            aria-label="Notifications"
            onKeyDown={handleMenuKeyDown}
            style={{ position: 'fixed', top: coords?.top ?? 0, left: coords?.left ?? 0 }}
            className="z-50 w-80 overflow-hidden rounded-card border border-brand-slate-200 bg-white py-1 text-left shadow-lg"
            data-testid="notification-bell-menu"
          >
            {items === null ? (
              <p className="px-4 py-3 text-sm text-brand-slate-500">Loading…</p>
            ) : items.length === 0 ? (
              <p className="px-4 py-3 text-sm text-brand-slate-500">No notifications yet.</p>
            ) : (
              items.map((n, index) => (
                <Link
                  key={n.id}
                  ref={(el) => {
                    itemRefs.current[index] = el;
                  }}
                  to={n.linkPath ?? '/notifications'}
                  role="menuitem"
                  tabIndex={-1}
                  onClick={() => handleItemClick(n)}
                  data-testid={`notification-bell-item-${n.id}`}
                  className={cn(
                    'block px-4 py-2.5 text-sm transition-colors hover:bg-brand-slate-50',
                    !n.readAt && 'bg-brand-teal-50/60'
                  )}
                >
                  <p className="truncate font-medium text-brand-slate-800">{n.title}</p>
                  <p className="mt-0.5 text-xs text-brand-slate-500">{formatDate(n.createdAt)}</p>
                </Link>
              ))
            )}
            <Link
              ref={(el) => {
                itemRefs.current[notificationItemCount] = el;
              }}
              to="/notifications"
              role="menuitem"
              tabIndex={-1}
              // Deferred for the same reason as `handleItemClick`: closing
              // synchronously would unmount this very link before React
              // Router's own handler gets to `preventDefault()` and navigate.
              onClick={() => setTimeout(() => setOpen(false), 0)}
              className="block border-t border-brand-slate-100 px-4 py-2 text-center text-sm font-medium text-brand-teal-600 hover:bg-brand-slate-50"
            >
              See all
            </Link>
          </div>,
          document.body
        )}
    </div>
  );
}
