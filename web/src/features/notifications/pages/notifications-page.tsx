import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Bell } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/toast';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { usePageTitle } from '@/hooks/use-page-title';
import { listNotifications, markAllNotificationsRead, markNotificationRead } from '../api/notifications-api';
import { useNotificationsContext } from '../hooks/use-notifications-context';
import type { NotificationDto } from '../types';

// A server-provided message is already resolved text and shown as-is; the
// generic fallback is translated at RENDER time (see `error` below) rather
// than load time, so a language switch after a failed load shows the new
// language immediately, with no refetch (phase 2 review).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

export function NotificationsPage() {
  const { t } = useTranslation(['notifications', 'common']);
  usePageTitle(t('notificationsPage.pageTitle'));
  const { show: showToast } = useToast();
  // The sidebar bell shares this same count (see `NotificationsProvider`) —
  // refreshed below after a successful mark-read/mark-all so the badge in
  // this same viewport updates immediately instead of waiting for the next
  // 60s poll tick.
  const { refresh: refreshUnreadCount } = useNotificationsContext();
  const [items, setItems] = useState<NotificationDto[] | null>(null);
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  const [markingAll, setMarkingAll] = useState(false);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listNotifications({ limit: 100 });
        if (!active) return;
        if (response.success && response.data) {
          setItems(response.data.items);
          setLoadError(null);
        } else {
          setLoadError(response.message ? { kind: 'server', message: response.message } : { kind: 'generic' });
        }
      } catch (err) {
        if (!active) return;
        const serverMessage = apiErrorMessage(err, '');
        setLoadError(serverMessage ? { kind: 'server', message: serverMessage } : { kind: 'generic' });
      }
    })();
    return () => {
      active = false;
    };
    // `t` deliberately excluded — see the `LoadError` comment above.
  }, [retryToken]);

  const error = loadError
    ? loadError.kind === 'server'
      ? loadError.message
      : t('notificationsPage.loadFailed')
    : null;

  const handleMarkRead = async (notification: NotificationDto) => {
    if (notification.readAt) return;
    try {
      await markNotificationRead(notification.id);
      setItems(
        (prev) =>
          prev?.map((n) => (n.id === notification.id ? { ...n, readAt: new Date().toISOString() } : n)) ?? prev
      );
      refreshUnreadCount();
    } catch (err) {
      showToast({ message: apiErrorMessage(err, t('notificationsPage.markReadFailed')), variant: 'error' });
    }
  };

  const handleMarkAll = async () => {
    setMarkingAll(true);
    try {
      const response = await markAllNotificationsRead();
      if (response.success) {
        setItems((prev) => prev?.map((n) => ({ ...n, readAt: n.readAt ?? new Date().toISOString() })) ?? prev);
        showToast({ message: t('notificationsPage.markAllReadSuccess'), variant: 'success' });
        refreshUnreadCount();
      } else {
        showToast({ message: response.message ?? t('notificationsPage.markAllFailed'), variant: 'error' });
      }
    } catch (err) {
      showToast({ message: apiErrorMessage(err, t('notificationsPage.markAllFailed')), variant: 'error' });
    } finally {
      setMarkingAll(false);
    }
  };

  const hasUnread = (items ?? []).some((n) => !n.readAt);

  return (
    <PageLayout
      title={t('notificationsPage.pageTitle')}
      actions={
        <Button
          variant="secondary"
          size="sm"
          onClick={handleMarkAll}
          loading={markingAll}
          disabled={!hasUnread}
          data-testid="notifications-mark-all-read"
        >
          {t('notificationsPage.markAllRead')}
        </Button>
      }
    >
      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((n) => n + 1)}>
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}

      {!error && items === null && (
        <div className="space-y-2">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      )}

      {!error && items !== null && items.length === 0 && (
        <EmptyState
          icon={Bell}
          title={t('notificationsPage.emptyTitle')}
          description={t('notificationsPage.emptyDescription')}
        />
      )}

      {!error && items !== null && items.length > 0 && (
        <ul className="space-y-2" data-testid="notifications-list">
          {items.map((n) => {
            const body = (
              <Card
                className={n.readAt ? undefined : 'border-l-2 border-l-brand-teal-500'}
                data-testid={`notification-${n.id}`}
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="font-medium text-brand-slate-800">{n.title}</p>
                    <p className="mt-1 text-sm text-brand-slate-600">{n.body}</p>
                    <p className="mt-1 text-xs text-brand-slate-500">{formatDate(n.createdAt)}</p>
                    {n.emailError && (
                      <p className="mt-1 text-xs text-brand-danger-600">
                        {t('notificationsPage.emailFailedPrefix', { error: n.emailError })}
                      </p>
                    )}
                  </div>
                  {!n.readAt && <Badge variant="info">{t('notificationsPage.newBadge')}</Badge>}
                </div>
              </Card>
            );
            return (
              <li key={n.id}>
                {n.linkPath ? (
                  <Link
                    to={n.linkPath}
                    onClick={() => handleMarkRead(n)}
                    className="block rounded-card focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
                  >
                    {body}
                  </Link>
                ) : (
                  <button
                    type="button"
                    onClick={() => handleMarkRead(n)}
                    className="block w-full rounded-card text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-teal-500"
                  >
                    {body}
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </PageLayout>
  );
}
