import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { usePageTitle } from '@/hooks/use-page-title';
import { listNotificationFailures } from '../api/notifications-api';
import type { NotificationDto } from '../types';

// A server-provided message is already resolved text and shown as-is; the
// generic fallback is translated at RENDER time (see `error` below) rather
// than load time, so a language switch after a failed load shows the new
// language immediately, with no refetch (phase 2 review).
type LoadError = { kind: 'server'; message: string } | { kind: 'generic' };

/** Platform admin: notifications where the email send failed, newest first. */
export function AdminNotificationFailuresPage() {
  const { t } = useTranslation(['notifications', 'common']);
  usePageTitle(t('adminFailures.pageTitle'));
  const [items, setItems] = useState<NotificationDto[] | null>(null);
  const [loadError, setLoadError] = useState<LoadError | null>(null);
  // Bumped by the "Try again" button to re-run the load effect below.
  const [retryToken, setRetryToken] = useState(0);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const response = await listNotificationFailures();
        if (!active) return;
        if (response.success && response.data) {
          setItems(response.data);
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
      : t('adminFailures.loadFailed')
    : null;

  const columns: TableColumn<NotificationDto>[] = [
    { key: 'kind', header: t('adminFailures.columnKind'), cell: (n) => n.kind, sortValue: (n) => n.kind },
    { key: 'title', header: t('adminFailures.columnTitle'), cell: (n) => n.title, sortValue: (n) => n.title },
    {
      key: 'error',
      header: t('adminFailures.columnError'),
      cell: (n) => <span className="text-brand-danger-700">{n.emailError}</span>,
    },
    {
      key: 'created',
      header: t('adminFailures.columnCreated'),
      align: 'right',
      cell: (n) => formatDate(n.createdAt),
      sortValue: (n) => n.createdAt,
    },
  ];

  return (
    <PageLayout title={t('adminFailures.pageTitle')} subtitle={t('adminFailures.subtitle')}>
      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((n) => n + 1)}>
              {t('common:ui.tryAgain')}
            </Button>
          </Notice>
        </div>
      )}
      {!error && (
        <Table
          label={t('adminFailures.tableLabel')}
          columns={columns}
          rows={items ?? []}
          rowKey={(n) => n.id}
          loading={items === null}
          data-testid="notification-failures-table"
          empty={
            <EmptyState
              icon={AlertTriangle}
              title={t('adminFailures.emptyTitle')}
              description={t('adminFailures.emptyDescription')}
            />
          }
        />
      )}
    </PageLayout>
  );
}
