import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Link } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { cn } from '@/lib/cn';
import { formatDate } from '@/lib/format-date';
import { listNotifications, markNotificationRead } from '../api/notifications-api';
import { useNotificationUnreadCount } from '../hooks/use-notification-unread-count';
import type { NotificationDto } from '../types';

/**
 * Sidebar bell: unread badge (polled) + a dropdown of the latest 5 with a
 * "See all" link to `/notifications`. The dropdown is portaled to `<body>` so
 * it is never clipped by the sidebar's layout, matching `Menu`'s approach.
 */
export function NotificationBell() {
  const { unreadCount, refresh } = useNotificationUnreadCount();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<NotificationDto[] | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  // The dropdown is portaled to `<body>`, so it is NOT a DOM descendant of
  // `containerRef` — the outside-click check below must also exempt it, or a
  // pointerdown on any dropdown item (including its own real `<a>` targets)
  // closes the menu before the click ever reaches React's handler.
  const menuRef = useRef<HTMLDivElement>(null);
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
    markNotificationRead(notification.id)
      .then(refresh)
      .catch(() => undefined);
  };

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
            style={{ position: 'fixed', top: coords?.top ?? 0, left: coords?.left ?? 0 }}
            className="z-50 w-80 overflow-hidden rounded-card border border-brand-slate-200 bg-white py-1 text-left shadow-lg"
            data-testid="notification-bell-menu"
          >
            {items === null ? (
              <p className="px-4 py-3 text-sm text-brand-slate-400">Loading…</p>
            ) : items.length === 0 ? (
              <p className="px-4 py-3 text-sm text-brand-slate-400">No notifications yet.</p>
            ) : (
              items.map((n) => (
                <Link
                  key={n.id}
                  to={n.linkPath ?? '/notifications'}
                  role="menuitem"
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
              to="/notifications"
              role="menuitem"
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
