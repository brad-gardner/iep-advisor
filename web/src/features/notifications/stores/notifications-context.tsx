import { useEffect, useRef, useState } from 'react';
import { listNotifications } from '../api/notifications-api';
import { NotificationsContext, type NotificationsContextValue } from './notifications-context-value';

const POLL_INTERVAL_MS = 60_000;

/**
 * One shared unread-count poll for the whole app, mounted once (in
 * `MainLayout`) rather than per component. Without this, every mount of the
 * sidebar bell (it renders twice — a mobile drawer copy and a desktop rail
 * copy, see `sidebar.tsx`) started its own independent poll, and marking
 * something read from the full `/notifications` page had no way to reach
 * either bell's badge short of the next 60s tick.
 *
 * Polls `GET /api/notifications?unread=true&limit=1` every 60s — the
 * envelope's `unreadCount` reflects the total, independent of `limit`.
 * Polling pauses while the tab is hidden and resumes (with an immediate
 * refresh) when it becomes visible again.
 *
 * The fetch function is declared *inside* the effect (rather than via
 * `useCallback`/`useEffectEvent`) so every `setUnreadCount` call is visibly
 * gated behind its own `await` within the same scope the effect runs in.
 * `refresh()` (called after a mark-read/mark-all so a corrected count shows
 * immediately, not on the next poll tick) is a plain stable function backed
 * by a ref. A monotonically increasing generation counter, bumped at the
 * start of every fetch, ensures a slow poll that was already in flight when
 * `refresh()` fires a faster, newer fetch can never later overwrite that
 * newer (already-applied) result when it finally resolves.
 */
export function NotificationsProvider({ children }: { children: React.ReactNode }) {
  const [unreadCount, setUnreadCount] = useState<number | null>(null);
  const refreshRef = useRef<() => void>(() => {});
  const generationRef = useRef(0);

  useEffect(() => {
    let active = true;

    async function fetchUnreadCount() {
      if (document.hidden) return;
      const generation = ++generationRef.current;
      try {
        const response = await listNotifications({ unread: true, limit: 1 });
        if (!active || generation !== generationRef.current) return;
        if (response.success && response.data) setUnreadCount(response.data.unreadCount);
      } catch {
        // Leave the last known count; a transient failure shouldn't flicker the badge.
      }
    }

    refreshRef.current = () => void fetchUnreadCount();

    void fetchUnreadCount();
    const interval = setInterval(() => void fetchUnreadCount(), POLL_INTERVAL_MS);
    const onVisibility = () => {
      if (!document.hidden) void fetchUnreadCount();
    };
    document.addEventListener('visibilitychange', onVisibility);

    return () => {
      active = false;
      clearInterval(interval);
      document.removeEventListener('visibilitychange', onVisibility);
    };
  }, []);

  const value: NotificationsContextValue = { unreadCount, refresh: () => refreshRef.current() };
  return <NotificationsContext.Provider value={value}>{children}</NotificationsContext.Provider>;
}
