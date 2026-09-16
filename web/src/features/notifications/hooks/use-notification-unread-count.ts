import { useEffect, useRef, useState } from 'react';
import { listNotifications } from '../api/notifications-api';

const POLL_INTERVAL_MS = 60_000;

interface UseNotificationUnreadCountResult {
  /** `null` until the first successful fetch. */
  unreadCount: number | null;
  refresh: () => void;
}

/**
 * Polls `GET /api/notifications?unread=true&limit=1` every 60s for the
 * sidebar bell's unread badge — the envelope's `unreadCount` reflects the
 * total, independent of `limit`. Polling pauses while the tab is hidden and
 * resumes (with an immediate refresh) when it becomes visible again.
 *
 * The fetch function is declared *inside* the effect (rather than via
 * `useCallback`/`useEffectEvent`) so every `setUnreadCount` call is visibly
 * gated behind its own `await` within the same scope the effect runs in —
 * `refresh()` (exposed for "mark as read" to trigger an immediate recheck) is
 * a plain stable function backed by a ref, not an Effect Event, since Effect
 * Event handles cannot be returned from a hook.
 */
export function useNotificationUnreadCount(): UseNotificationUnreadCountResult {
  const [unreadCount, setUnreadCount] = useState<number | null>(null);
  const refreshRef = useRef<() => void>(() => {});

  useEffect(() => {
    let active = true;

    async function fetchUnreadCount() {
      if (document.hidden) return;
      try {
        const response = await listNotifications({ unread: true, limit: 1 });
        if (active && response.success && response.data) setUnreadCount(response.data.unreadCount);
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

  return { unreadCount, refresh: () => refreshRef.current() };
}
