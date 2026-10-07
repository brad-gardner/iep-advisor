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

/** Platform admin: notifications where the email send failed, newest first. */
export function AdminNotificationFailuresPage() {
  const { t } = useTranslation('notifications');
  usePageTitle(t('adminFailures.pageTitle'));
  const [items, setItems] = useState<NotificationDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
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
          setError(null);
        } else {
          setError(response.message ?? t('adminFailures.loadFailed'));
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, t('adminFailures.loadFailed')));
      }
    })();
    return () => {
      active = false;
    };
  }, [retryToken, t]);

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
