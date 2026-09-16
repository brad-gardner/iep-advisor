import { useEffect, useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { PageLayout } from '@/components/ui/page-layout';
import { Table, type TableColumn } from '@/components/ui/table';
import { apiErrorMessage } from '@/lib/api-error';
import { formatDate } from '@/lib/format-date';
import { listNotificationFailures } from '../api/notifications-api';
import type { NotificationDto } from '../types';

/** Platform admin: notifications where the email send failed, newest first. */
export function AdminNotificationFailuresPage() {
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
          setError(response.message ?? 'Could not load email failures');
        }
      } catch (err) {
        if (active) setError(apiErrorMessage(err, 'Could not load email failures'));
      }
    })();
    return () => {
      active = false;
    };
  }, [retryToken]);

  const columns: TableColumn<NotificationDto>[] = [
    { key: 'kind', header: 'Kind', cell: (n) => n.kind, sortValue: (n) => n.kind },
    { key: 'title', header: 'Title', cell: (n) => n.title, sortValue: (n) => n.title },
    {
      key: 'error',
      header: 'Error',
      cell: (n) => <span className="text-brand-danger-700">{n.emailError}</span>,
    },
    {
      key: 'created',
      header: 'Created',
      align: 'right',
      cell: (n) => formatDate(n.createdAt),
      sortValue: (n) => n.createdAt,
    },
  ];

  return (
    <PageLayout title="Notification email failures" subtitle="Notifications where the email send failed.">
      {error && (
        <div role="alert">
          <Notice variant="error" title={error}>
            <Button size="sm" variant="secondary" onClick={() => setRetryToken((t) => t + 1)}>
              Try again
            </Button>
          </Notice>
        </div>
      )}
      {!error && (
        <Table
          label="Email failures"
          columns={columns}
          rows={items ?? []}
          rowKey={(n) => n.id}
          loading={items === null}
          data-testid="notification-failures-table"
          empty={
            <EmptyState
              icon={AlertTriangle}
              title="No email failures"
              description="Every notification email has sent successfully."
            />
          }
        />
      )}
    </PageLayout>
  );
}
