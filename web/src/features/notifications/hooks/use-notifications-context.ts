import { useContext } from 'react';
import { NotificationsContext, type NotificationsContextValue } from '../stores/notifications-context-value';

/** Shared unread count + on-demand refresh — see `NotificationsProvider`. */
export function useNotificationsContext(): NotificationsContextValue {
  const ctx = useContext(NotificationsContext);
  if (!ctx) {
    throw new Error('useNotificationsContext must be used within a NotificationsProvider');
  }
  return ctx;
}
