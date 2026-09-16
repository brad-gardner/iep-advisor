import { createContext } from 'react';

export interface NotificationsContextValue {
  /** `null` until the first successful fetch. */
  unreadCount: number | null;
  refresh: () => void;
}

/**
 * Split into its own file (no components here) so the fast-refresh lint rule
 * doesn't flag `notifications-context.tsx` for exporting both a context and
 * a component — see `NotificationsProvider` there and `useNotificationsContext`
 * in `hooks/use-notifications-context.ts`.
 */
export const NotificationsContext = createContext<NotificationsContextValue | null>(null);
